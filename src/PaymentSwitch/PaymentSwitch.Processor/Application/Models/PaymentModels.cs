namespace PaymentSwitch.Processor.Application.Models;

public sealed record ConfirmedQuoteComponent(
	string ComponentType,
	string Description,
	string Source,
	bool VisibleToClient,
	int DisplayOrder,
	long MinorUnits,
	string Currency);

public sealed record ConfirmedQuoteReference(string Key, string Value);

public sealed record VerifiedBill(string? ErrorCode, string ProviderInquiryId, string ObservedBillKey,
	long AmountMinorUnits, string Currency, DateTime? CheckedAt, DateTime? ExpiresAt, string? BillingPeriod = null);

public sealed record ConfirmedQuoteExecution(
	string ClientId,
	Guid SourceQuoteId,
	Guid CreditCardId,
	Guid ServiceId,
	string ServiceName,
	Guid CountryId,
	string CountryCode,
	Guid RouteId,
	string RouteType,
	Guid? PaymentNetworkId,
	Guid? ProviderId,
	string? ExternalSourceCode,
	string ExternalServiceCode,
	string CompanyPaymentEndpoint,
	string CompanyRollbackEndpoint,
	string? CompanyStatusEndpoint,
	string CompanyCredentialReference,
	DateTime QuoteCreatedAt,
	DateTime QuoteExpiresAt,
	string RoundingMode,
	long TotalMinorUnits,
	string Currency,
	IReadOnlyCollection<ConfirmedQuoteComponent> Components,
	IReadOnlyCollection<ConfirmedQuoteReference> ReferenceValues,
	Guid? SubmissionId = null,
	DateTime? SubmittedAt = null,
	string? BillKey = null,
	VerifiedBill? BillVerification = null)
{
	public bool RequiresBillVerification => BillKey is not null || BillVerification is not null ||
		Components.Any(component => component.Source == "AmountInquiry");
}

public enum PaymentExecutionResultStatus
{
	Processing,
	Completed,
	Declined,
	RollbackPending,
	Failed,
	ManualIntervention,
	RefundPending,
	Refunded
}

public sealed record PaymentExecutionResult(
	bool Accepted,
	Guid? PaymentId,
	string? Reference,
	PaymentExecutionResultStatus Status,
	string? ErrorCode,
	string? ErrorMessage,
	bool Replayed,
	bool Final,
	bool SubmissionRecorded = false);

public sealed class PaymentExecutionOptions
{
	public const string SectionName = "PaymentExecution";

	public int RetryIntervalSeconds { get; init; } = 300;
	public int MaxCardCheckAttempts { get; init; } = 3;
	public int MaxCompanyPaymentAttempts { get; init; } = 3;
	public int MaxChargeAttempts { get; init; } = 3;
	public int MaxRollbackAttempts { get; init; } = 10;
	public int MaxCompensationAttempts { get; init; } = 5;
	public int DispatcherIntervalSeconds { get; init; } = 10;
	public int DispatcherBatchSize { get; init; } = 25;
}
