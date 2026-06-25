using Infrastructure.RabbitMQ;
using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Worker.Application.Abstractions;
using PaymentSwitch.Worker.Interfaces;

namespace PaymentSwitch.Worker.Application.UseCases
{
	public sealed class ProcessRefundRequestUseCase(
		IRefundGateway refundGateway,
		IPaymentStatusRepository paymentStatus,
		IMessagePublisher messagePublisher,
		IClock clock,
		ILogger<ProcessRefundRequestUseCase> logger)
	{
		private const int MaxRetries = 3;

		private readonly IRefundGateway _refundGateway = refundGateway;
		private readonly IPaymentStatusRepository _paymentStatus = paymentStatus;
		private readonly IMessagePublisher _messagePublisher = messagePublisher;
		private readonly IClock _clock = clock;
		private readonly ILogger<ProcessRefundRequestUseCase> _logger = logger;

		public async Task ExecuteAsync(
			RefundRequestMessage message,
			CancellationToken cancellationToken = default)
		{
			_logger.LogInformation(
				"Processing refund for payment {PaymentId}, attempt {Retry}",
				message.PaymentId,
				message.RetryCount + 1);

			if (string.IsNullOrWhiteSpace(message.OperatorProvider))
			{
				throw new InvalidOperationException(
					"Refund request does not contain an operator provider.");
			}

			var result = await _refundGateway.RefundAsync(
				message.OperatorProvider,
				message.OperatorPaymentId,
				message.Amount,
				message.Currency,
				message.Reason);

			if (result.Success)
			{
				await CompleteRefundAsync(message);
				return;
			}

			if (message.RetryCount < MaxRetries - 1)
			{
				await ScheduleRetryAsync(message, cancellationToken);
				return;
			}

			await PublishFailedRefundAlertAsync(message);
		}

		private async Task CompleteRefundAsync(RefundRequestMessage message)
		{
			await _paymentStatus.UpdateStatusAsync(
				PaymentTransactionStatus.Refunded,
				message.PaymentId);

			await _messagePublisher.PublishAsync(
				new EmailMessage
				{
					To = string.Empty,
					Subject = "Payment refunded",
					Body = $"Your payment has been refunded. Reason: {message.Reason}",
					CreatedAt = _clock.UtcNow
				},
				PaymentSwitchQueues.Exchanges.Notifications,
				PaymentSwitchQueues.Queues.EmailSend);

			_logger.LogInformation(
				"Refund completed for payment {PaymentId}",
				message.PaymentId);
		}

		private async Task ScheduleRetryAsync(
			RefundRequestMessage message,
			CancellationToken cancellationToken)
		{
			_logger.LogWarning(
				"Refund attempt {Retry}/{Max} failed for {PaymentId}",
				message.RetryCount + 1,
				MaxRetries,
				message.PaymentId);

			await _paymentStatus.ScheduleRefundRetryAsync(
				message.PaymentId,
				_clock.UtcNow.AddMinutes(5));
		}

		private async Task PublishFailedRefundAlertAsync(RefundRequestMessage message)
		{
			_logger.LogError(
				"Refund failed after {Max} attempts for payment {PaymentId}",
				MaxRetries,
				message.PaymentId);

			await _paymentStatus.UpdateStatusAsync(
				PaymentTransactionStatus.RefundFailed,
				message.PaymentId,
				errorMessage: $"Refund failed after {MaxRetries} attempts");

			await _messagePublisher.PublishAsync(
				new AlertMessage
				{
					PaymentId = message.PaymentId,
					Message =
						$"URGENT: Refund failed after {MaxRetries} attempts for payment {message.PaymentId}. Manual intervention required.",
					Severity = "Critical",
					CreatedAt = _clock.UtcNow
				},
				PaymentSwitchQueues.Exchanges.Notifications,
				PaymentSwitchQueues.Queues.AlertCreate);
		}
	}
}
