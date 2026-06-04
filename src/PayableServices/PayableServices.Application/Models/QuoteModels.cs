namespace PayableServices.Application.Models;

public sealed record QuoteLineDto(
	Guid Id,
	string Description,
	decimal Amount,
	string Currency);

public sealed record QuoteDto(
	Guid Id,
	string ClientId,
	Guid ServiceId,
	string ServiceName,
	string Currency,
	string Status,
	decimal TotalAmount,
	DateTime CreatedAt,
	DateTime ExpiresAt,
	IReadOnlyCollection<QuoteLineDto> Items);

public sealed record CreateQuoteCommand(
	string ClientId,
	Guid ServiceId,
	string Currency);

public sealed record CreateQuoteResult(
	bool Success,
	QuoteDto? Quote,
	string? ErrorMessage);
