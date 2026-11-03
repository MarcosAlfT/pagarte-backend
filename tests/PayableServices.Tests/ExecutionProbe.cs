using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using Utilities.Money;

namespace PayableServices.Tests;

// Exercise the unchanged Processor execution validator against the actual immutable handoff.
public sealed class ExecutionProbe : IPaymentExecutionClient
{
    public QuoteExecutionPackage? Package;
    public Task<ConfirmQuoteResult> ConfirmQuoteAsync(QuoteExecutionPackage package, CancellationToken cancellationToken = default)
    {
        Package = package;
        var execution = new ConfirmedQuoteExecution(package.ClientId, package.SourceQuoteId, package.CreditCardId,
            package.ServiceId, package.ServiceName, package.CountryId, package.CountryCode, package.RouteId,
            package.RouteType, package.PaymentNetworkId, package.ProviderId, package.ExternalSourceCode,
            package.ExternalServiceCode, package.CompanyPaymentEndpoint, package.CompanyRollbackEndpoint,
            package.CompanyStatusEndpoint, package.CompanyCredentialReference, package.CreatedAt, package.ExpiresAt,
            package.RoundingMode, package.TotalMinorUnits, package.Currency,
            package.Components.Select(x => new ConfirmedQuoteComponent(x.ComponentType, x.Description, x.Source,
                x.VisibleToClient, x.DisplayOrder, x.MinorUnits, x.Currency)).ToArray(),
            package.ReferenceValues.Select(x => new ConfirmedQuoteReference(x.Key, x.Value)).ToArray());
        Assert.Null(new ConfirmedQuoteValidator(new CurrencyCatalog()).Validate(execution, package.CreatedAt));
        return Task.FromResult(new ConfirmQuoteResult(true, Guid.NewGuid(), "execution-reference", QuoteExecutionStatus.Completed, null, null, false, true));
    }
    public Task<ConfirmQuoteResult> GetStatusAsync(string clientId, Guid sourceQuoteId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
