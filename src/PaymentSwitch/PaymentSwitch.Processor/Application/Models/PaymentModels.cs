using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Models
{
	public sealed record CreatePaymentQuoteCommand(
		string ClientId,
		Guid ServiceId,
		string Currency);

	public sealed record PaymentQuoteResult(
		bool Success,
		PaymentQuote? Quote,
		string? ErrorMessage);

	public sealed record ConfirmPaymentCommand(
		string ClientId,
		Guid QuoteId,
		Guid CreditCardId);

	public sealed record PaymentResult(
		bool Success,
		Guid? PaymentId,
		string? Reference,
		string? Status,
		string? ErrorMessage);
}
