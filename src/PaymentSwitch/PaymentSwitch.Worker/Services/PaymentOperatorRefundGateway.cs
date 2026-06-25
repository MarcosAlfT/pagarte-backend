using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Worker.Application.Abstractions;

namespace PaymentSwitch.Worker.Services
{
	public sealed class PaymentOperatorRefundGateway(
		IPaymentOperatorAdapterFactory paymentOperatorAdapterFactory)
		: IRefundGateway
	{
		private readonly IPaymentOperatorAdapterFactory _paymentOperatorAdapterFactory =
			paymentOperatorAdapterFactory;

		public async Task<RefundGatewayResult> RefundAsync(
			string operatorProvider,
			string operatorPaymentId,
			decimal amount,
			string currency,
			string reason)
		{
			var paymentOperatorAdapter =
				_paymentOperatorAdapterFactory.GetRequiredAdapter(operatorProvider);

			var result = await paymentOperatorAdapter.RefundAsync(
				operatorPaymentId,
				amount,
				currency,
				reason);

			return new RefundGatewayResult(result.Success, result.ErrorMessage);
		}
	}
}
