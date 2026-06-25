using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Models
{
	public sealed record RegisterCreditCardCommand(
		string ClientId,
		string CardNumber,
		string Cvv,
		string CardHolderName,
		int ExpiryMonth,
		int ExpiryYear,
		bool IsDefault);

	public sealed record RegisterCreditCardResult(
		bool Success,
		CreditCard? Card,
		string? CardType,
		string? ErrorMessage);

	public sealed record UpdateCreditCardCommand(
		string ClientId,
		Guid CardId,
		string CardHolderName,
		bool IsDefault);

	public sealed record DeleteCreditCardCommand(
		string ClientId,
		Guid CardId);
}
