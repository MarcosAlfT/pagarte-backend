namespace PaymentSwitch.Worker.Application.Abstractions
{
	public interface IRefundGateway
	{
		Task<RefundGatewayResult> RefundAsync(
			string operatorProvider,
			string operatorPaymentId,
			long minorUnits,
			string currency,
			string reason);
	}

	public sealed record RefundGatewayResult(bool Success, string? ErrorMessage);
}
