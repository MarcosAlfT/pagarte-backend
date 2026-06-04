namespace Payments.Api.DTOs
{
	public record RegisterCreditCardRequest(
		string CardNumber,
		string Cvv,
		string CardHolderName,
		int ExpiryMonth,
		int ExpiryYear,
		bool IsDefault);

	public record UpdateCreditCardRequest(
		string CardHolderName,
		bool IsDefault);
}
