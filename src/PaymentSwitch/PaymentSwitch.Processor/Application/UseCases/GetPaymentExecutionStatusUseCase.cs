using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class GetPaymentExecutionStatusUseCase(
	IPaymentRepository paymentRepository, IQuoteSubmissionRepository submissions)
{
	public async Task<PaymentExecutionResult> ExecuteAsync(
		string clientId,
		Guid sourceQuoteId,
		CancellationToken cancellationToken = default)
	{
		var payment = await paymentRepository.GetBySourceQuoteIdAsync(
			sourceQuoteId,
			cancellationToken);
		if (payment is not null)
			return payment.ClientId == clientId ? PaymentExecutionResultMapper.Map(payment) : Missing();

		var recorded = await submissions.GetAsync(sourceQuoteId, cancellationToken);
		if (recorded is null || recorded.ClientId != clientId) return Missing();
		// Acceptance may have committed between the two reads.
		if (recorded.PaymentId is { } paymentId)
		{
			var accepted = await paymentRepository.GetByIdAsync(paymentId, cancellationToken)
				?? throw new InvalidOperationException("Recorded payment is missing.");
			return PaymentExecutionResultMapper.Map(accepted);
		}
		return new(false, null, null, PaymentExecutionResultStatus.Failed,
			recorded.ErrorCode, recorded.ErrorMessage, true, true, SubmissionRecorded: true);
	}

	private static PaymentExecutionResult Missing() =>
		new(false, null, null, PaymentExecutionResultStatus.Failed,
			"EXECUTION_NOT_FOUND", "Payment execution was not found.", false, true);
}
