namespace PayableServices.Application.Models;

public sealed record ConfirmQuoteCommand(
	string ClientId,
	Guid QuoteId,
	Guid CreditCardId);

public sealed record ConfirmQuoteResult(
	bool Success,
	Guid? PaymentId,
	string? Reference,
	string? Status,
	string? ErrorMessage);
