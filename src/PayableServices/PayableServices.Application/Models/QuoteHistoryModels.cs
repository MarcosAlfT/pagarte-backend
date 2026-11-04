using PayableServices.Domain.Enums;

namespace PayableServices.Application.Models;

public sealed record QuoteHistoryQuery(
    QuoteStatus? Status = null,
    Guid? ServiceId = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null,
    int Page = 1,
    int PageSize = 20)
{
    public bool IsValid =>
        Page is > 0 and <= 1000000 && PageSize is > 0 and <= 100 &&
        (Status is null || Enum.IsDefined(Status.Value)) &&
        ServiceId != Guid.Empty &&
        (CreatedFrom is null || CreatedTo is null || CreatedFrom <= CreatedTo);
}

public sealed record QuoteSummaryDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string Status,
    decimal TotalAmount,
    long TotalMinorUnits,
    string Currency,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Guid? PaymentId,
    Guid? InquiryId,
    IReadOnlyCollection<QuoteReferenceValueDto> ReferenceValues);

public sealed record QuoteHistoryResponse(
    IReadOnlyCollection<QuoteSummaryDto> Quotes,
    int Page,
    int PageSize);
