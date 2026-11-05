using System.Text.Json;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Application.Services;
using PayableServices.Domain.Entities;

namespace PayableServices.Application.UseCases;

public sealed class VerifySubmittedBillUseCase(IInquiryRepository inquiries, QuoteRouteEligibility routeEligibility,
    IAmountInquiryProvider provider, IClock clock, IUnitOfWork unitOfWork)
{
    public async Task<BillVerification> ExecuteAsync(Quote quote, CancellationToken cancellationToken)
    {
        var bill = quote.BillIdentityJson is { } json ? JsonSerializer.Deserialize<BillIdentity>(json) : null;
        if (bill is null || quote.InquiryId is not { } inquiryId ||
            await inquiries.GetAsync(inquiryId, cancellationToken) is not { } inquiry ||
            inquiry.ClientId != quote.ClientId || inquiry.QuoteId != quote.Id || inquiry.RouteId != quote.RouteId)
            return new("BILL_IDENTITY_REQUIRED");

        if (!await routeEligibility.IsEligibleAsync(quote, inquiry.Provider, cancellationToken))
            return new("BILL_ROUTE_UNAVAILABLE");

        var references = inquiry.References();
        var operationKey = Guid.NewGuid().ToString("N");
        var requestedAt = clock.UtcNow;
        var result = await provider.InquireAsync(new(inquiry.Provider, quote.ExternalServiceCode,
            references, operationKey, quote.Currency, requestedAt), cancellationToken);
        var now = clock.UtcNow;
        if (result.Outcome is not (InquiryOutcome.AmountDue or InquiryOutcome.NothingDue) ||
            result.ProviderInquiryId == inquiry.ProviderInquiryId ||
            !ProviderBalanceValidation.Matches(result, quote.ExternalServiceCode, references, requestedAt, now))
            return new("BILL_VERIFICATION_UNAVAILABLE", operationKey);

        var observed = BillIdentityStrategyRegistry.Select(inquiry.Provider, quote.ExternalServiceCode, references, result.BillingPeriod);
        if (observed is null)
            return new("BILL_VERIFICATION_UNAVAILABLE", operationKey, result.ProviderInquiryId);

        await using var transaction = await unitOfWork.BeginAsync("catalogue", cancellationToken);
        if (!await routeEligibility.IsEligibleAsync(quote, inquiry.Provider, cancellationToken))
            return new("BILL_ROUTE_UNAVAILABLE", operationKey, result.ProviderInquiryId);

        var checkedAt = result.ResultAt!.Value;
        var expiry = checkedAt.AddMinutes(1);
        if (result.ExpiresAt!.Value < expiry) expiry = result.ExpiresAt.Value;
        var error = observed.Key != bill.Key || result.Currency != quote.Currency
            ? "BILL_CHANGED"
            : result.Outcome == InquiryOutcome.NothingDue
                ? "BILL_NO_LONGER_DUE"
                : result.AmountMinorUnits != inquiry.AmountMinorUnits ? "BILL_CHANGED" : null;
        return new(error, operationKey, result.ProviderInquiryId, observed.Key, result.AmountMinorUnits,
            result.Currency, checkedAt, expiry, observed.BillingPeriod);
    }
}
