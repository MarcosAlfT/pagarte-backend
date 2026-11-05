using PayableServices.Domain.Entities;

namespace PayableServices.Application.Models;

public sealed record ReferenceValue(string Key, string Value);
public sealed record CreateInquiryCommand(string ClientId, Guid ServiceId, string IdempotencyKey,
    IReadOnlyCollection<ReferenceValue> References);
public sealed record ProviderInquiryRequest(string Provider, string ExternalServiceCode,
    IReadOnlyDictionary<string, string> References, string ProviderKey, string Currency, DateTime RequestedAt);
public sealed record ProviderInquiryResult(InquiryOutcome Outcome, string? ProviderInquiryId = null,
    string? ExternalServiceCode = null, IReadOnlyDictionary<string, string>? References = null,
    long? AmountMinorUnits = null, string? Currency = null, DateTime? ResultAt = null,
    DateTime? ExpiresAt = null, DateTime? DueDate = null, string? ErrorCode = null,
    string? BillingPeriod = null);
public sealed record InquiryDto(Guid Id, Guid ServiceId, string Outcome,
    IReadOnlyDictionary<string, string> References, long? AmountMinorUnits, string? Currency,
    string? ProviderInquiryId, DateTime CreatedAt, DateTime? ProviderResultAt,
    DateTime? ExpiresAt, DateTime? DueDate, string? ErrorCode, Guid? QuoteId, bool Expired)
{
    public static InquiryDto Map(AmountInquiry inquiry, DateTime now) => new(
        inquiry.Id, inquiry.ServiceId, inquiry.Outcome.ToString(), inquiry.References(),
        inquiry.AmountMinorUnits, inquiry.Currency, inquiry.ProviderInquiryId, inquiry.CreatedAt,
        inquiry.ProviderResultAt, inquiry.ExpiresAt, inquiry.DueDate, inquiry.ErrorCode,
        inquiry.QuoteId, inquiry.ExpiresAt <= now);
}
public sealed record InquiryResult(InquiryDto? Inquiry, string? ErrorCode = null);
