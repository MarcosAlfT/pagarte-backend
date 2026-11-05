using PayableServices.Domain.Enums;

namespace PayableServices.Domain.Entities;

public sealed class Quote
{
    public Guid? InquiryId { get; set; }
    public string? BillIdentityJson { get; set; }
    public string? BillVerificationJson { get; private set; }
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public Guid ServiceId { get; set; }
    public Guid CountryId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public Guid RouteId { get; set; }
    public string RouteType { get; set; } = string.Empty;
    public Guid? PaymentNetworkId { get; set; }
    public Guid? ProviderId { get; set; }
    public string? ExternalSourceCode { get; set; }
    public string ExternalServiceCode { get; set; } = string.Empty;
    public string CompanyPaymentEndpoint { get; set; } = string.Empty;
    public string CompanyRollbackEndpoint { get; set; } = string.Empty;
    public string? CompanyStatusEndpoint { get; set; }
    public string CompanyCredentialReference { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public int CurrencyScale { get; set; }
    public string RoundingMode { get; set; } = string.Empty;
    public QuoteStatus Status { get; set; } = QuoteStatus.Unpaid;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public decimal TotalAmount { get; set; }
    public long TotalMinorUnits { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public int LifecycleVersion { get; set; }
    public Guid? SubmissionId { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public Guid? SubmittedCardId { get; private set; }
    public string? SubmissionPackageJson { get; private set; }
    public string? SubmissionFingerprint { get; private set; }
    public Guid? SubmissionLeaseId { get; private set; }
    public DateTime? SubmissionLeaseUntil { get; private set; }
    public DateTime? SubmissionNextAttemptAt { get; private set; }
    public DateTime? SubmissionCompletedAt { get; private set; }
    public string? SubmissionResultJson { get; private set; }
    public int SubmissionAttemptCount { get; private set; }
    public string? ExecutionErrorCode { get; set; }
    public string? ExecutionErrorMessage { get; set; }
    public ICollection<QuoteItem> Items { get; set; } = [];
    public ICollection<QuoteReferenceValue> ReferenceValues { get; set; } = [];

    // This is a read projection, not proof that an uncertain submission cannot exist.
    public QuoteStatus GetEffectiveStatus(DateTime utcNow) =>
        Status == QuoteStatus.Unpaid && SubmittedAt is null && PaymentId is null && utcNow >= ExpiresAt
            ? QuoteStatus.Expired
            : Status;

    public bool CanSubmit(DateTime utcNow) => LifecycleVersion == 1 &&
        Status == QuoteStatus.Unpaid && SubmissionId is null && PaymentId is null && utcNow < ExpiresAt;

    public void Submit(Guid submissionId, Guid cardId, DateTime utcNow)
    {
        if (!CanSubmit(utcNow) || submissionId == Guid.Empty || cardId == Guid.Empty)
            throw new InvalidOperationException("Quote cannot start another submission.");
        SubmissionId = submissionId;
        SubmittedCardId = cardId;
        SubmittedAt = utcNow;
        Status = QuoteStatus.Submitted;
        SubmissionNextAttemptAt = utcNow;
    }

    public void BindSubmissionPackage(string json, string fingerprint)
    {
        if (SubmissionId is null || SubmissionPackageJson is not null)
            throw new InvalidOperationException("Submission package is immutable.");
        SubmissionPackageJson = json;
        SubmissionFingerprint = fingerprint;
    }

    public Guid? ClaimSubmission(DateTime utcNow)
    {
        if (SubmissionId is null || SubmissionCompletedAt is not null ||
            SubmissionLeaseUntil > utcNow || SubmissionNextAttemptAt > utcNow) return null;
        SubmissionLeaseId = Guid.NewGuid();
        SubmissionLeaseUntil = utcNow.AddMinutes(1);
        SubmissionAttemptCount++;
        return SubmissionLeaseId;
    }

    public bool OwnsSubmissionLease(Guid leaseId) => SubmissionCompletedAt is null && SubmissionLeaseId == leaseId;

    public void RecordBillVerification(Guid leaseId, string json)
    {
        if (!OwnsSubmissionLease(leaseId)) throw new InvalidOperationException("Submission lease is stale.");
        BillVerificationJson = json;
    }

    public void FinishSubmissionWork(Guid leaseId, string? resultJson, bool final, DateTime utcNow)
    {
        if (!OwnsSubmissionLease(leaseId)) throw new InvalidOperationException("Submission lease is stale.");
        ObserveSubmissionResult(resultJson, final, utcNow);
        SubmissionLeaseId = null;
        SubmissionLeaseUntil = null;
    }

    public void ObserveSubmissionResult(string? resultJson, bool final, DateTime utcNow)
    {
        if (SubmissionCompletedAt is not null) return;
        if (resultJson is not null) SubmissionResultJson = resultJson;
        SubmissionCompletedAt = final ? utcNow : null;
        SubmissionNextAttemptAt = final ? null : utcNow.AddSeconds(10);
        if (final)
        {
            SubmissionLeaseId = null;
            SubmissionLeaseUntil = null;
        }
    }
}
