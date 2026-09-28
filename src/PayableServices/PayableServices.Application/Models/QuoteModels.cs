namespace PayableServices.Application.Models;

public sealed record QuoteLineDto(
	Guid Id,
	string ComponentType,
	string Description,
	string Source,
	bool IsVisibleToClient,
	int DisplayOrder,
	decimal Amount,
	long MinorUnits,
	string Currency);

public sealed record QuoteReferenceValueDto(string Key, string Value);

public sealed record QuoteDto(
	Guid Id,
	string ClientId,
	Guid ServiceId,
	string ServiceName,
	Guid CountryId,
	string CountryCode,
	string Currency,
	int CurrencyScale,
	string RoundingMode,
	string Status,
	decimal TotalAmount,
	long TotalMinorUnits,
	DateTime CreatedAt,
	DateTime ExpiresAt,
	Guid? PaymentId,
	IReadOnlyCollection<QuoteLineDto> Items,
	IReadOnlyCollection<QuoteReferenceValueDto> ReferenceValues);

public sealed record CreateQuoteCommand(
	string ClientId,
	Guid ServiceId,
	string Currency,
	IReadOnlyDictionary<string, string> ReferenceValues);

public sealed record CreateQuoteResult(
	bool Success,
	QuoteDto? Quote,
	string? ErrorMessage);
