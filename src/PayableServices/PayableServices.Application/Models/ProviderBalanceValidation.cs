using PayableServices.Domain.Entities;

namespace PayableServices.Application.Models;

internal static class ProviderBalanceValidation
{
    public static bool Matches(ProviderInquiryResult result, string serviceCode,
        IReadOnlyDictionary<string, string> references, DateTime requestedAt, DateTime now) =>
        result.Currency is { Length: 3 } currency && currency.All(c => c is >= 'A' and <= 'Z') &&
        !string.IsNullOrWhiteSpace(result.ProviderInquiryId) && result.ProviderInquiryId.Length <= 200 &&
        result.ExternalServiceCode == serviceCode && result.References is not null &&
        result.References.Count == references.Count &&
        references.All(x => result.References.TryGetValue(x.Key, out var value) && value == x.Value) &&
        (result.Outcome == InquiryOutcome.AmountDue ? result.AmountMinorUnits > 0 : result.AmountMinorUnits == 0) &&
        result.ResultAt is { Kind: DateTimeKind.Utc } checkedAt && result.ExpiresAt is { Kind: DateTimeKind.Utc } &&
        checkedAt >= requestedAt && checkedAt <= now && checkedAt <= DateTime.MaxValue.AddMinutes(-15) &&
        result.ExpiresAt > checkedAt && result.ExpiresAt > now && checkedAt.AddMinutes(15) > now &&
        (result.DueDate is null || result.DueDate.Value.Kind == DateTimeKind.Utc);
}
