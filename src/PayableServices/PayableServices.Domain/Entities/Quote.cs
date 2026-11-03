using PayableServices.Domain.Enums;

namespace PayableServices.Domain.Entities;

public sealed class Quote
{
    public Guid? InquiryId { get; set; }
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
    public string? ExecutionErrorCode { get; set; }
    public string? ExecutionErrorMessage { get; set; }
    public ICollection<QuoteItem> Items { get; set; } = [];
    public ICollection<QuoteReferenceValue> ReferenceValues { get; set; } = [];
}
