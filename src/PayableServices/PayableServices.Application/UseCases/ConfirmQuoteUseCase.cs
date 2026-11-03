using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;
using PayableServices.Domain.Enums;

namespace PayableServices.Application.UseCases;

public sealed class ConfirmQuoteUseCase(
    IQuoteRepository quoteRepository,
    IPaymentExecutionClient paymentExecutionClient,
    IClock clock, IUnitOfWork unitOfWork)
{
    public async Task<ConfirmQuoteResult> ExecuteAsync(
        ConfirmQuoteCommand request,
        CancellationToken cancellationToken = default)
    {
        var quote = await quoteRepository.GetByIdAsync(request.QuoteId, cancellationToken);
        if (quote is null || quote.ClientId != request.ClientId)
        {
            return Failure("QUOTE_NOT_FOUND", "Quote not found.");
        }

        if (quote.Status is not (QuoteStatus.Unpaid or QuoteStatus.Processing))
        {
            return Failure("QUOTE_NOT_PAYABLE", "Quote is not payable.");
        }

        if (clock.UtcNow >= quote.ExpiresAt && quote.Status == QuoteStatus.Unpaid)
        {
            quote.Status = QuoteStatus.Expired;
            await quoteRepository.UpdateAsync(quote, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Failure("QUOTE_EXPIRED", "Quote expired.");
        }

        var result = await paymentExecutionClient.ConfirmQuoteAsync(
            MapExecutionPackage(quote, request.CreditCardId),
            cancellationToken);

        ApplyExecutionResult(quote, result);
        await quoteRepository.UpdateAsync(quote, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
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

        if (quote.PaymentId is null)
        {
            return Failure("EXECUTION_NOT_STARTED", "Quote execution has not started.");
        }

        var result = await paymentExecutionClient.GetStatusAsync(
            clientId,
            quoteId,
            cancellationToken);
        ApplyExecutionResult(quote, result);
        await quoteRepository.UpdateAsync(quote, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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
                .ToArray());

    private static void ApplyExecutionResult(Quote quote, ConfirmQuoteResult result)
    {
        quote.PaymentId = result.PaymentId ?? quote.PaymentId;
        quote.ExecutionErrorCode = result.ErrorCode;
        quote.ExecutionErrorMessage = result.ErrorMessage;
        if (!result.Accepted && result.PaymentId is null)
        {
            return;
        }

        quote.Status = result.Status switch
        {
            QuoteExecutionStatus.Completed => QuoteStatus.Paid,
            QuoteExecutionStatus.Declined or QuoteExecutionStatus.Failed
                when result.Final => QuoteStatus.PaymentFailed,
            _ => QuoteStatus.Processing
        };
    }

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
