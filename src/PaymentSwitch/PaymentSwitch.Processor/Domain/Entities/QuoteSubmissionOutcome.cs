namespace PaymentSwitch.Processor.Domain.Entities;

// Written in the same acceptance transaction as the payment, or as a rejection tombstone.
public sealed class QuoteSubmissionOutcome
{
    public Guid SourceQuoteId { get; set; }
    public Guid SubmissionId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string PackageFingerprint { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime RecordedAt { get; set; }
}
