using ExternalConnections.CompanyPayments;
using Microsoft.Extensions.Configuration;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using Utilities.Money;

namespace PaymentSwitch.Processor.Infrastructure.Gateways;

public sealed class CompanyPaymentGateway(
	ICompanyAdapter companyAdapter,
	IConfiguration configuration,
	ICurrencyCatalog currencyCatalog) : ICompanyPaymentGateway
{
	public async Task<PaymentOperationResult> PayServiceAsync(
		Payment payment,
		CancellationToken cancellationToken = default)
	{
		var apiKey = ResolveApiKey(payment.CompanyCredentialReference);
		if (apiKey is null)
		{
			return MissingCredentials();
		}

		var result = await companyAdapter.PayServiceAsync(
			new CompanyPaymentRequest(
				payment.CompanyPaymentEndpoint,
				apiKey,
				payment.ExternalServiceCode,
				payment.ReferenceValues.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
				GetMoney(payment),
				payment.Reference,
				payment.ClientId,
				payment.CompanyPaymentIdempotencyKey),
			cancellationToken);
		return Map(result);
	}

	public async Task<PaymentOperationResult> ReconcilePaymentAsync(
		Payment payment,
		CancellationToken cancellationToken = default)
	{
		var apiKey = ResolveApiKey(payment.CompanyCredentialReference);
		if (apiKey is null)
		{
			return MissingCredentials();
		}

		if (string.IsNullOrWhiteSpace(payment.CompanyReference))
		{
			return new PaymentOperationResult(
				PaymentOperationOutcome.Unknown,
				null,
				"COMPANY_REFERENCE_UNAVAILABLE",
				"The company payment has no external reference; its idempotent request must be replayed.");
		}

		return Map(await companyAdapter.GetOperationStatusAsync(
			new CompanyStatusRequest(
				payment.CompanyStatusEndpoint ?? string.Empty,
				apiKey,
				payment.CompanyReference,
				payment.CompanyPaymentIdempotencyKey),
			cancellationToken));
	}

	public async Task<PaymentOperationResult> RollbackAsync(
		Payment payment,
		CancellationToken cancellationToken = default)
	{
		var apiKey = ResolveApiKey(payment.CompanyCredentialReference);
		if (apiKey is null)
		{
			return MissingCredentials();
		}

		if (string.IsNullOrWhiteSpace(payment.CompanyReference))
		{
			return new PaymentOperationResult(
				PaymentOperationOutcome.DefinitiveFailure,
				null,
				"COMPANY_REFERENCE_MISSING",
				"The accepted company payment does not have a rollback reference.");
		}

		return Map(await companyAdapter.RollbackServicePaymentAsync(
			new CompanyRollbackRequest(
				payment.CompanyRollbackEndpoint,
				apiKey,
				payment.ExternalServiceCode,
				payment.CompanyReference,
				GetMoney(payment),
				payment.Reference,
				"Card charge did not complete before the configured attempt cap.",
				payment.CompanyRollbackIdempotencyKey),
			cancellationToken));
	}

	private MoneyValue GetMoney(Payment payment) =>
		MoneyValue.FromMinorUnits(
			payment.TotalMinorUnits,
			payment.Currency,
			currencyCatalog);

	private string? ResolveApiKey(string reference)
	{
		var value = configuration[$"CompanyCredentials:{reference}:ApiKey"];
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	private static PaymentOperationResult MissingCredentials() =>
		new(
			PaymentOperationOutcome.DefinitiveFailure,
			null,
			"COMPANY_CREDENTIALS_MISSING",
			"Company credentials are not configured for the selected route.");

	private static PaymentOperationResult Map(CompanyOperationResult result) =>
		new(
			result.Outcome switch
			{
				CompanyOperationOutcome.Succeeded => PaymentOperationOutcome.Succeeded,
				CompanyOperationOutcome.DefinitiveFailure => PaymentOperationOutcome.DefinitiveFailure,
				CompanyOperationOutcome.RetryableFailure => PaymentOperationOutcome.RetryableFailure,
				_ => PaymentOperationOutcome.Unknown
			},
			result.CompanyReference,
			result.ErrorCode,
			result.ErrorMessage);
}
