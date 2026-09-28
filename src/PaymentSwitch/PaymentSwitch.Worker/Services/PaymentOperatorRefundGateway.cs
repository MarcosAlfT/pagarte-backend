using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Worker.Application.Abstractions;
using Utilities.Money;

namespace PaymentSwitch.Worker.Services
{
	public sealed class PaymentOperatorRefundGateway(
		IPaymentOperatorAdapterFactory paymentOperatorAdapterFactory,
		ICurrencyCatalog currencyCatalog)
		: IRefundGateway
	{
		private readonly IPaymentOperatorAdapterFactory _paymentOperatorAdapterFactory =
			paymentOperatorAdapterFactory;

		public async Task<RefundGatewayResult> RefundAsync(
			string operatorProvider,
			string operatorPaymentId,
			long minorUnits,
			string currency,
			string reason)
		{
			var paymentOperatorAdapter =
				_paymentOperatorAdapterFactory.GetRequiredAdapter(operatorProvider);

			var money = MoneyValue.FromMinorUnits(minorUnits, currency, currencyCatalog);
			var result = await paymentOperatorAdapter.RefundAsync(
				operatorPaymentId,
				money.Amount,
				money.Currency,
				reason,
				$"refund-{operatorPaymentId}"[..Math.Min(42, 7 + operatorPaymentId.Length)]);

			return new RefundGatewayResult(result.Success, result.ErrorMessage);
		}
	}
}
