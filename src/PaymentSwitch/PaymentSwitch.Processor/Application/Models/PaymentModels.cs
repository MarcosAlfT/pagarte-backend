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
	IReadOnlyCollection<ConfirmedQuoteReference> ReferenceValues);

public enum PaymentExecutionResultStatus
{
	Processing,
	Completed,
	Declined,
	RollbackPending,
	Failed,
	ManualIntervention
}

public sealed record PaymentExecutionResult(
	bool Accepted,
	Guid? PaymentId,
	string? Reference,
	PaymentExecutionResultStatus Status,
	string? ErrorCode,
	string? ErrorMessage,
	bool Replayed,
	bool Final);

public sealed class PaymentExecutionOptions
{
	public const string SectionName = "PaymentExecution";

	public int RetryIntervalSeconds { get; init; } = 300;
	public int MaxCardCheckAttempts { get; init; } = 3;
	public int MaxCompanyPaymentAttempts { get; init; } = 3;
	public int MaxChargeAttempts { get; init; } = 3;
	public int MaxRollbackAttempts { get; init; } = 10;
	public int DispatcherIntervalSeconds { get; init; } = 10;
	public int DispatcherBatchSize { get; init; } = 25;
}
