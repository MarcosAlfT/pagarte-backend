namespace PayableServices.Application.Models;

public sealed record ConfirmQuoteCommand(
	string ClientId,
	Guid QuoteId,
	Guid CreditCardId);

public enum QuoteExecutionStatus
{
	Processing,
	Completed,
	Declined,
	RollbackPending,
	Failed,
	ManualIntervention
}

public sealed record ExecutionAmountComponent(
	string ComponentType,
	string Description,
	string Source,
	bool VisibleToClient,
	int DisplayOrder,
	long MinorUnits,
	string Currency);

public sealed record ExecutionReferenceValue(string Key, string Value);

public sealed record QuoteExecutionPackage(
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
	DateTime CreatedAt,
	DateTime ExpiresAt,
	string RoundingMode,
	long TotalMinorUnits,
	string Currency,
	IReadOnlyCollection<ExecutionAmountComponent> Components,
	IReadOnlyCollection<ExecutionReferenceValue> ReferenceValues);

public sealed record ConfirmQuoteResult(
	bool Accepted,
	Guid? PaymentId,
	string? Reference,
	QuoteExecutionStatus Status,
	string? ErrorCode,
	string? ErrorMessage,
	bool Replayed,
	bool Final);
