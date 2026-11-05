using ExternalConnections.CompanyPayments;
using ExternalConnections.PaymentOperators.Config;
using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Utilities.Money;

namespace PaymentSwitch.Processor.Infrastructure.Gateways;

public sealed class ChargeFirstPaymentGateway(IHostEnvironment environment, IConfiguration configuration,
    PaymentOperatorRegistry registry, IPaymentOperatorResolver operators,
    CompanyNotificationAdapter company, ICurrencyCatalog currencies) : IChargeFirstPaymentGateway
{
    public string? ResolveNotificationEndpoint(ConfirmedQuoteExecution execution, string operatorProvider)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("ChargeFirst:Enabled") ||
            execution.BillKey is null || execution.ExternalSourceCode != "MockCompany" ||
            execution.ExternalServiceCode is not ("ELEC-EC" or "WATER-EC" or "NET-US") ||
            execution.CompanyCredentialReference != "MockCompany" ||
            string.IsNullOrWhiteSpace(configuration["CompanyCredentials:MockCompany:ApiKey"]) ||
            operatorProvider.Equals("Mock", StringComparison.OrdinalIgnoreCase) ||
            registry.GetRequired(operatorProvider).Adapter != "Mock" ||
            !Uri.TryCreate(configuration["ChargeFirst:MockCompanyBaseUrl"]?.TrimEnd('/') + "/", UriKind.Absolute, out var companyBase) ||
            companyBase.Scheme is not ("http" or "https") ||
            execution.CompanyPaymentEndpoint != new Uri(companyBase, "payments").AbsoluteUri)
            return null;

        return new Uri(companyBase, "payment-notifications").AbsoluteUri;
    }

    public async Task<PaymentOperationResult> NotifyAsync(Payment payment, bool recover, CancellationToken cancellationToken)
    {
        var apiKey = configuration[$"CompanyCredentials:{payment.CompanyCredentialReference}:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(PaymentOperationOutcome.Unknown, null, "COMPANY_CREDENTIALS_UNAVAILABLE", "Company credentials are unavailable.");

        var serviceAmount = payment.Details.Single(detail => detail.ComponentType == "ServiceAmount" && detail.Source == "AmountInquiry");
        var notice = new CompanyBillNotification(payment.BillKey!, payment.ExternalServiceCode,
            payment.ReferenceValues.Single(reference => reference.Key == "accountNumber").Value, payment.BillingPeriod!,
            serviceAmount.MinorUnits, serviceAmount.Currency, payment.Reference, payment.OperatorPaymentId!);
        var result = await company.NotifyAsync(payment.CompanyNotificationEndpoint!, apiKey,
            payment.CompanyPaymentIdempotencyKey, notice, recover, cancellationToken);
        return new(result.Outcome switch
        {
            CompanyOperationOutcome.Succeeded => PaymentOperationOutcome.Succeeded,
            CompanyOperationOutcome.DefinitiveFailure => PaymentOperationOutcome.DefinitiveFailure,
            _ => PaymentOperationOutcome.Unknown
        }, result.CompanyReference, result.ErrorCode, result.ErrorMessage);
    }

    public async Task<PaymentOperationResult> RefundAsync(Payment payment, CancellationToken cancellationToken)
    {
        var money = MoneyValue.FromMinorUnits(payment.TotalMinorUnits, payment.Currency, currencies);
        if (operators.GetAdapter(payment.OperatorProvider) is not MockPaymentOperatorAdapter mock)
            return new(PaymentOperationOutcome.Unknown, null, "REFUND_PROVIDER_UNAVAILABLE", "The pinned refund provider is unavailable.");
        var result = await mock.RefundConfirmedChargeAsync(payment.OperatorPaymentId!,
            money.Amount, money.Currency, payment.CompensationKey!, cancellationToken);
        return new(result.Outcome switch
        {
            PaymentOperatorOutcome.Succeeded when result.RefundId is { Length: > 0 and <= 200 } => PaymentOperationOutcome.Succeeded,
            PaymentOperatorOutcome.DefinitiveDecline => PaymentOperationOutcome.DefinitiveFailure,
            _ => PaymentOperationOutcome.Unknown
        }, result.RefundId, result.ErrorCode, result.ErrorMessage);
    }
}
