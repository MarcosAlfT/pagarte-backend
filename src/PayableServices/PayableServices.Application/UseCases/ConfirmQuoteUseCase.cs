using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;
using PayableServices.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PayableServices.Application.UseCases;

public sealed class ConfirmQuoteUseCase(
    IQuoteRepository quoteRepository,
    IPaymentExecutionClient paymentExecutionClient,
    IClock clock, IUnitOfWork unitOfWork, VerifySubmittedBillUseCase verifyBill)
{
    public async Task<ConfirmQuoteResult> ExecuteAsync(
        ConfirmQuoteCommand request,
        CancellationToken cancellationToken = default)
    {
        if (request.CreditCardId == Guid.Empty) return Failure("CARD_INVALID", "Select a stored card.");
        Quote quote;
        Guid lease;
        await using (var transaction = await unitOfWork.BeginAsync("quote:" + request.QuoteId, cancellationToken))
        {
            var owned = await quoteRepository.GetByIdAsync(request.QuoteId, cancellationToken);
            if (owned is null || owned.ClientId != request.ClientId)
                return Failure("QUOTE_NOT_FOUND", "Quote not found.");
            quote = owned;
            var now = clock.UtcNow;
            if (quote.SubmissionId is null)
            {
                if (quote.GetEffectiveStatus(now) == QuoteStatus.Expired)
                {
                    quote.Status = QuoteStatus.Expired;
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return Failure("QUOTE_EXPIRED", "Quote expired; consult again.");
                }
                if (!quote.CanSubmit(now))
                    return Failure("QUOTE_NOT_PAYABLE", "Quote cannot start another payment; consult again.");
                quote.Submit(Guid.NewGuid(), request.CreditCardId, now);
                var json = JsonSerializer.Serialize(MapExecutionPackage(quote, request.CreditCardId));
                quote.BindSubmissionPackage(json, Fingerprint(json));
            }
            else if (quote.SubmittedCardId != request.CreditCardId)
                return Failure("SUBMISSION_CONFLICT", "Quote is already consumed by another selected card.");

            if (quote.SubmissionCompletedAt is not null) return StoredResult(quote);
            var claimed = quote.ClaimSubmission(clock.UtcNow);
            if (claimed is null) return Pending(quote);
            lease = claimed.Value;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await DispatchAsync(quote, lease, cancellationToken);
    }

    public async Task RecoverAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        Quote quote;
        Guid lease;
        await using (var transaction = await unitOfWork.BeginAsync("quote:" + quoteId, cancellationToken))
        {
            var pending = await quoteRepository.GetByIdAsync(quoteId, cancellationToken);
            if (pending is null) return;
            var claimed = pending.ClaimSubmission(clock.UtcNow);
            if (claimed is null) return;
            quote = pending;
            lease = claimed.Value;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await DispatchAsync(quote, lease, cancellationToken);
    }

    private async Task<ConfirmQuoteResult> DispatchAsync(Quote snapshot, Guid lease, CancellationToken cancellationToken)
    {
        ConfirmQuoteResult result;
        try
        {
            var json = snapshot.SubmissionPackageJson ?? throw new InvalidOperationException("Submission snapshot is missing.");
            if (Fingerprint(json) != snapshot.SubmissionFingerprint)
                throw new InvalidOperationException("Submission snapshot fingerprint is invalid.");
            var package = JsonSerializer.Deserialize<QuoteExecutionPackage>(json)
                ?? throw new InvalidOperationException("Submission snapshot is invalid.");
            // Lookup first; accepted work must not be submitted again just because its local commit was lost.
            result = await paymentExecutionClient.GetStatusAsync(snapshot.ClientId, snapshot.Id, cancellationToken);
            var awaitingAcceptance = !result.Accepted && result.PaymentId is null &&
                result.ErrorCode == "EXECUTION_NOT_FOUND" && snapshot.PaymentId is null;
            if (snapshot.InquiryId is not null && (awaitingAcceptance || result.ErrorCode == "BILL_VERIFICATION_STALE"))
            {
                var previous = snapshot.BillVerificationJson is { } evidence
                    ? JsonSerializer.Deserialize<BillVerification>(evidence) : null;
                var verification = previous is { ErrorCode: not null }
                    ? previous : await verifyBill.ExecuteAsync(snapshot, cancellationToken);
                if (!await RecordVerificationAsync(snapshot.Id, lease, verification, cancellationToken))
                    return Pending(snapshot);
                result = await paymentExecutionClient.ConfirmQuoteAsync(package with { BillVerification = verification }, cancellationToken);
            }
            else if (awaitingAcceptance)
                result = await paymentExecutionClient.ConfirmQuoteAsync(package, cancellationToken);
            if (!result.Accepted && result.PaymentId is null && !(result.Final && result.SubmissionRecorded))
                result = Pending(snapshot);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Unknown dispatch/status is recoverable with the exact same immutable package and identity.
            result = Pending(snapshot);
        }

        await using var transaction = await unitOfWork.BeginAsync("quote:" + snapshot.Id, cancellationToken);
        var quote = await quoteRepository.GetByIdAsync(snapshot.Id, cancellationToken)
            ?? throw new InvalidOperationException("Submission quote is missing.");
        if (!quote.OwnsSubmissionLease(lease)) return StoredResult(quote);
        ApplyExecutionResult(quote, result);
        quote.FinishSubmissionWork(lease, JsonSerializer.Serialize(result), result.Final, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<bool> RecordVerificationAsync(Guid quoteId, Guid lease,
        BillVerification verification, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginAsync("quote:" + quoteId, cancellationToken);
        var quote = await quoteRepository.GetByIdAsync(quoteId, cancellationToken);
        if (quote is null || !quote.OwnsSubmissionLease(lease) || quote.SubmissionLeaseUntil <= clock.UtcNow)
            return false;
        quote.RecordBillVerification(lease, JsonSerializer.Serialize(verification));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ConfirmQuoteResult> RefreshStatusAsync(
        string clientId,
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var quote = await quoteRepository.GetByIdAsync(quoteId, cancellationToken);
        if (quote is null || quote.ClientId != clientId)
        {
            return Failure("QUOTE_NOT_FOUND", "Quote not found.");
        }

        ConfirmQuoteResult result;
        try
        {
            result = await paymentExecutionClient.GetStatusAsync(clientId, quoteId, cancellationToken);
        }
        catch (Exception) when (quote.SubmissionId is not null && !cancellationToken.IsCancellationRequested)
        {
            return StoredResult(quote);
        }
        // Processor may have accepted before the response or local commit was lost.
        // A missing execution does not authorize a new submission or cancellation.
        if ((!result.Accepted && result.PaymentId is null) || result.ErrorCode == "BILL_VERIFICATION_STALE")
        {
            return quote.SubmissionId is not null ? StoredResult(quote) : result;
        }

        await using var transaction = await unitOfWork.BeginAsync("quote:" + quoteId, cancellationToken);
        quote = await quoteRepository.GetByIdAsync(quoteId, cancellationToken)
            ?? throw new InvalidOperationException("Quote is missing.");
        if (quote.SubmissionCompletedAt is not null) return StoredResult(quote);
        // Terminal legacy states are also protected against delayed status responses.
        if (quote.Status is QuoteStatus.Paid or QuoteStatus.PaymentFailed) return result;
        ApplyExecutionResult(quote, result);
        if (quote.SubmissionId is not null)
            quote.ObserveSubmissionResult(JsonSerializer.Serialize(result), result.Final, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static QuoteExecutionPackage MapExecutionPackage(Quote quote, Guid creditCardId) =>
        new(
            quote.ClientId,
            quote.Id,
            creditCardId,
            quote.ServiceId,
            quote.ServiceName,
            quote.CountryId,
            quote.CountryCode,
            quote.RouteId,
            quote.RouteType,
            quote.PaymentNetworkId,
            quote.ProviderId,
            quote.ExternalSourceCode,
            quote.ExternalServiceCode,
            quote.CompanyPaymentEndpoint,
            quote.CompanyRollbackEndpoint,
            quote.CompanyStatusEndpoint,
            quote.CompanyCredentialReference,
            quote.CreatedAt,
            quote.ExpiresAt,
            quote.RoundingMode,
            quote.TotalMinorUnits,
            quote.Currency,
            quote.Items
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new ExecutionAmountComponent(
                    item.ComponentType,
                    item.Description,
                    item.Source,
                    item.IsVisibleToClient,
                    item.DisplayOrder,
                    item.MinorUnits,
                    item.Currency))
                .ToArray(),
            quote.ReferenceValues
                .OrderBy(reference => reference.Key, StringComparer.Ordinal)
                .Select(reference => new ExecutionReferenceValue(reference.Key, reference.Value))
                .ToArray(), quote.SubmissionId, quote.SubmittedAt,
            quote.BillIdentityJson is { } json ? JsonSerializer.Deserialize<BillIdentity>(json)?.Key : null);

    private static void ApplyExecutionResult(Quote quote, ConfirmQuoteResult result)
    {
        quote.PaymentId = result.PaymentId ?? quote.PaymentId;
        quote.ExecutionErrorCode = result.ErrorCode;
        quote.ExecutionErrorMessage = result.ErrorMessage;
        if (result.Final && (result.SubmissionRecorded || result.PaymentId is not null))
        {
            if (result.ErrorCode == "BILL_CHANGED") { quote.Status = QuoteStatus.Invalidated; return; }
            if (result.ErrorCode == "BILL_NO_LONGER_DUE") { quote.Status = QuoteStatus.NoLongerDue; return; }
        }
        if (!result.Accepted && result.PaymentId is null)
        {
            if (result.Final && result.SubmissionRecorded) quote.Status = QuoteStatus.PaymentFailed;
            return;
        }

        if (result.PaymentId is null) return;

        quote.Status = result.Status switch
        {
            QuoteExecutionStatus.Completed => QuoteStatus.Paid,
            QuoteExecutionStatus.Declined or QuoteExecutionStatus.Failed
                when result.Final => QuoteStatus.PaymentFailed,
            _ => QuoteStatus.Processing
        };
    }

    private static string Fingerprint(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    private static ConfirmQuoteResult StoredResult(Quote quote) =>
        quote.SubmissionResultJson is { } json
            ? JsonSerializer.Deserialize<ConfirmQuoteResult>(json)! with { Replayed = true }
            : Pending(quote);

    private static ConfirmQuoteResult Pending(Quote quote) => new(
        true, quote.PaymentId, null, QuoteExecutionStatus.Processing,
        "SUBMISSION_PENDING", "The original payment submission is processing.", true, false);

    private static ConfirmQuoteResult Failure(string code, string message) =>
        new(
            false,
            null,
            null,
            QuoteExecutionStatus.Failed,
            code,
            message,
            false,
            true);
}
