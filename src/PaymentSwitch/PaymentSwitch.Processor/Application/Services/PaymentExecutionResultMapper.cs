using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Services;

public static class PaymentExecutionResultMapper
{
	public static PaymentExecutionResult Map(Payment payment, bool replayed = false)
	{
		var resultStatus = payment.Status switch
		{
			PaymentTransactionStatus.Completed => PaymentExecutionResultStatus.Completed,
			PaymentTransactionStatus.Failed => PaymentExecutionResultStatus.Declined,
			PaymentTransactionStatus.CompanyRolledBack => PaymentExecutionResultStatus.Failed,
			PaymentTransactionStatus.Compensated => PaymentExecutionResultStatus.Refunded,
			PaymentTransactionStatus.CompensationPending or PaymentTransactionStatus.RefundingCompensation =>
				PaymentExecutionResultStatus.RefundPending,
			PaymentTransactionStatus.ManualIntervention when payment.ChargeFirst => PaymentExecutionResultStatus.Processing,
			PaymentTransactionStatus.ManualIntervention =>
				PaymentExecutionResultStatus.ManualIntervention,
			PaymentTransactionStatus.RollingBackCompany
				or PaymentTransactionStatus.RollbackRetryScheduled =>
				PaymentExecutionResultStatus.RollbackPending,
			_ => PaymentExecutionResultStatus.Processing
		};
		var accepted = payment.Status != PaymentTransactionStatus.Failed;
		return new PaymentExecutionResult(
			accepted,
			payment.Id,
			payment.Reference,
			resultStatus,
			payment.ErrorCode,
			payment.ErrorMessage,
			replayed,
			payment.IsFinal && !(payment.ChargeFirst && payment.Status == PaymentTransactionStatus.ManualIntervention));
	}
}
