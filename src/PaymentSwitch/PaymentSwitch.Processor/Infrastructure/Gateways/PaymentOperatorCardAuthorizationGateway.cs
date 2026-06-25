using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Infrastructure.Gateways
{
	public sealed class PaymentOperatorCardAuthorizationGateway(
		IPaymentOperatorResolver paymentOperatorResolver)
		: ICardAuthorizationGateway
	{
		private readonly IPaymentOperatorResolver _paymentOperatorResolver =
			paymentOperatorResolver;

		public async Task<string> EnsureOperatorProviderAsync(CreditCard card)
		{
			if (!string.IsNullOrWhiteSpace(card.OperatorProvider))
			{
				return card.OperatorProvider;
			}

			var paymentOperator =
				await _paymentOperatorResolver.ResolveInternationalAsync();

			card.OperatorProvider = paymentOperator.ProviderCode;
			return paymentOperator.ProviderCode;
		}

		public async Task<CardAuthorizationResult> AuthorizeAsync(
			string operatorProvider,
			string operatorCardToken,
			decimal amount,
			string currency,
			string reference)
		{
			var adapter = _paymentOperatorResolver.GetAdapter(operatorProvider);
			var chargeResult = await adapter.ChargeAsync(
				operatorCardToken,
				amount,
				currency,
				reference);

			return new CardAuthorizationResult(
				chargeResult.Success,
				chargeResult.OperatorPaymentId,
				chargeResult.ErrorMessage);
		}
	}
}
