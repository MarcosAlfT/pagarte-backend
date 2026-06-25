namespace PaymentSwitch.Processor.Application.Abstractions
{
	public interface ICreditCardRegistrationGateway
	{
		Task<CreditCardRegistrationGatewayResult> RegisterAsync(
			string cardNumber,
			string cvv,
			string cardHolderName,
			int expiryMonth,
			int expiryYear);
	}

	public sealed record CreditCardRegistrationGatewayResult(
		bool Success,
		string? ProviderCode,
		string? CardToken,
		string? Last4Digits,
		string? CardType,
		int ExpiryMonth,
		int ExpiryYear,
		string? ErrorMessage);
}
