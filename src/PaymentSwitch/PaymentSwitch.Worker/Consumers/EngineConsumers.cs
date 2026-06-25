using Infrastructure.RabbitMQ;
using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Worker.Application.UseCases;

namespace PaymentSwitch.Worker.Consumers
{
	public class PaymentRequestConsumer(
		RabbitMqConnectionFactory connectionFactory,
		ProcessPaymentRequestUseCase processPaymentRequestUseCase,
		ILogger<PaymentRequestConsumer> logger)
		: BaseConsumer<PaymentRequestMessage>(connectionFactory, logger)
	{
		private readonly ProcessPaymentRequestUseCase _processPaymentRequestUseCase =
			processPaymentRequestUseCase;

		protected override string QueueName => PaymentSwitchQueues.Queues.PaymentRequest;

		protected override async Task HandleAsync(
			PaymentRequestMessage message,
			CancellationToken cancellationToken)
		{
			await _processPaymentRequestUseCase.ExecuteAsync(
				message,
				cancellationToken);
		}
	}

	public class RefundConsumer(
		RabbitMqConnectionFactory connectionFactory,
		ProcessRefundRequestUseCase processRefundRequestUseCase,
		ILogger<RefundConsumer> logger)
		: BaseConsumer<RefundRequestMessage>(connectionFactory, logger)
	{
		private readonly ProcessRefundRequestUseCase _processRefundRequestUseCase =
			processRefundRequestUseCase;

		protected override string QueueName => PaymentSwitchQueues.Queues.RefundRequest;

		protected override async Task HandleAsync(
			RefundRequestMessage message,
			CancellationToken cancellationToken)
		{
			await _processRefundRequestUseCase.ExecuteAsync(
				message,
				cancellationToken);
		}
	}

	public class EmailConsumer(
		RabbitMqConnectionFactory connectionFactory,
		SendPaymentEmailUseCase sendPaymentEmailUseCase,
		ILogger<EmailConsumer> logger)
		: BaseConsumer<EmailMessage>(connectionFactory, logger)
	{
		private readonly SendPaymentEmailUseCase _sendPaymentEmailUseCase =
			sendPaymentEmailUseCase;

		protected override string QueueName => PaymentSwitchQueues.Queues.EmailSend;

		protected override async Task HandleAsync(
			EmailMessage message,
			CancellationToken cancellationToken)
		{
			await _sendPaymentEmailUseCase.ExecuteAsync(message, cancellationToken);
		}
	}
}
