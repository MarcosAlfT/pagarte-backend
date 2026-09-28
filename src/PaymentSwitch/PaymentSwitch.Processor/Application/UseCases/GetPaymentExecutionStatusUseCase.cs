using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class GetPaymentExecutionStatusUseCase(IPaymentRepository paymentRepository)
{
	public async Task<PaymentExecutionResult> ExecuteAsync(
		string clientId,
		Guid sourceQuoteId,
		CancellationToken cancellationToken = default)
	{
		var payment = await paymentRepository.GetBySourceQuoteIdAsync(
			sourceQuoteId,
			cancellationToken);
		if (payment is null || payment.ClientId != clientId)
		{
			return new PaymentExecutionResult(
				false,
				null,
				null,
				PaymentExecutionResultStatus.Failed,
				"EXECUTION_NOT_FOUND",
				"Payment execution was not found.",
				false,
				true);
		}

		return PaymentExecutionResultMapper.Map(payment);
	}
}
