using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using Utilities.Money;
using System.Collections.Concurrent;

namespace PayableServices.Tests;

// Exercise the unchanged Processor execution validator against the actual immutable handoff.
public sealed class ExecutionProbe : IPaymentExecutionClient
{
    public QuoteExecutionPackage? Package;
    public int ConfirmCalls;
    public int StatusCalls;
    public bool StatusUnavailable;
    public Func<DateTime>? Now;
    public bool ConfirmUnavailable;
    public TaskCompletionSource? Entered;
    public TaskCompletionSource? Release;
    public Func<QuoteExecutionPackage, CancellationToken, Task<ConfirmQuoteResult>>? ConfirmOverride;
    public readonly ConcurrentDictionary<(string ClientId, Guid QuoteId), ConfirmQuoteResult> Results = new();
    public async Task<ConfirmQuoteResult> ConfirmQuoteAsync(QuoteExecutionPackage package, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref ConfirmCalls);
        Package = package;
        Entered?.TrySetResult();
        if (Release is not null) await Release.Task.WaitAsync(cancellationToken);
        if (ConfirmUnavailable) throw new HttpRequestException("Synthetic Processor dispatch outage.");
        var execution = new ConfirmedQuoteExecution(package.ClientId, package.SourceQuoteId, package.CreditCardId,
            package.ServiceId, package.ServiceName, package.CountryId, package.CountryCode, package.RouteId,
            package.RouteType, package.PaymentNetworkId, package.ProviderId, package.ExternalSourceCode,
            package.ExternalServiceCode, package.CompanyPaymentEndpoint, package.CompanyRollbackEndpoint,
            package.CompanyStatusEndpoint, package.CompanyCredentialReference, package.CreatedAt, package.ExpiresAt,
            package.RoundingMode, package.TotalMinorUnits, package.Currency,
            package.Components.Select(x => new ConfirmedQuoteComponent(x.ComponentType, x.Description, x.Source,
                x.VisibleToClient, x.DisplayOrder, x.MinorUnits, x.Currency)).ToArray(),
            package.ReferenceValues.Select(x => new ConfirmedQuoteReference(x.Key, x.Value)).ToArray(), package.SubmissionId, package.SubmittedAt);
        Assert.Null(new ConfirmedQuoteValidator(new CurrencyCatalog()).Validate(execution, Now?.Invoke() ?? package.SubmittedAt ?? package.CreatedAt));
        if (ConfirmOverride is not null) return await ConfirmOverride(package, cancellationToken);
        var result = Results.GetOrAdd((package.ClientId, package.SourceQuoteId),
            _ => package.BillVerification?.ErrorCode is { } code
                ? new(false, null, null, QuoteExecutionStatus.Failed, code, "Bill cannot be paid.", false, true, true)
                : new(true, Guid.NewGuid(), "execution-reference", QuoteExecutionStatus.Completed, null, null, false, true, true));
        return result;
    }
    public Task<ConfirmQuoteResult> GetStatusAsync(string clientId, Guid sourceQuoteId, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref StatusCalls);
        if (StatusUnavailable) throw new HttpRequestException("Synthetic Processor status outage.");
        return Task.FromResult(Results.TryGetValue((clientId, sourceQuoteId), out var result) && result.PaymentId is not null
            ? result with { Replayed = true }
            : new ConfirmQuoteResult(false, null, null, QuoteExecutionStatus.Failed,
                "EXECUTION_NOT_FOUND", "Payment execution was not found.", false, true));
    }
}
