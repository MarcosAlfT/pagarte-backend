using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Infrastructure.Gateways
{
	public sealed class PaymentOperatorCreditCardRegistrationGateway(
		IPaymentOperatorResolver paymentOperatorResolver)
		: ICreditCardRegistrationGateway
	{
		private readonly IPaymentOperatorResolver _paymentOperatorResolver =
			paymentOperatorResolver;

		public async Task<CreditCardRegistrationGatewayResult> RegisterAsync(
			string cardNumber,
			string cvv,
			string cardHolderName,
			int expiryMonth,
			int expiryYear)
		{
			var paymentOperator =
				await _paymentOperatorResolver.ResolveInternationalAsync();

			var result = await paymentOperator.Adapter.RegisterCardAsync(
				cardNumber,
				cvv,
				cardHolderName,
				expiryMonth,
				expiryYear);

			return new CreditCardRegistrationGatewayResult(
				result.Success,
				paymentOperator.ProviderCode,
				result.CardToken,
				result.Last4Digits,
				result.CardType,
				result.ExpiryMonth,
				result.ExpiryYear,
				result.ErrorMessage);
		}
	}
}
