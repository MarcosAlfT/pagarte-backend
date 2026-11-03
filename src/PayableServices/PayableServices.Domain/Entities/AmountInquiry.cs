using System.Text.Json;

namespace PayableServices.Domain.Entities;

public enum InquiryOutcome { Pending, AmountDue, NothingDue, ReferenceNotFound, TemporarilyUnavailable, InvalidProviderResponse }

public sealed class AmountInquiry
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = "";
    public Guid ServiceId { get; set; }
    public Guid RouteId { get; set; }
    public string Provider { get; set; } = "";
    public string ExternalServiceCode { get; set; } = "";
    public string ReferenceSnapshot { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string ProviderKey { get; set; } = "";
    public Guid LeaseToken { get; set; }
    public DateTime LeaseUntil { get; set; }
    public InquiryOutcome Outcome { get; set; }
    public long? AmountMinorUnits { get; set; }
    public string? Currency { get; set; }
    public string? ProviderInquiryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProviderResultAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ErrorCode { get; set; }
    public Guid? QuoteId { get; set; }

    public IReadOnlyDictionary<string, string> References() =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(ReferenceSnapshot)!;

    public bool CanQuote(string clientId, DateTime now) =>
        ClientId == clientId && Outcome == InquiryOutcome.AmountDue &&
        AmountMinorUnits > 0 && Currency == "USD" && ExpiresAt > now;
}
