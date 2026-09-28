using Infrastructure.RabbitMQ;
using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Worker.Application.UseCases;

namespace PaymentSwitch.Worker.Consumers;

public sealed class RefundConsumer(
	RabbitMqConnectionFactory connectionFactory,
	ProcessRefundRequestUseCase processRefundRequestUseCase,
	ILogger<RefundConsumer> logger)
	: BaseConsumer<RefundRequestMessage>(connectionFactory, logger)
{
	protected override string QueueName => PaymentSwitchQueues.Queues.RefundRequest;

	protected override Task HandleAsync(
		RefundRequestMessage message,
		CancellationToken cancellationToken) =>
		processRefundRequestUseCase.ExecuteAsync(message, cancellationToken);
}

public sealed class EmailConsumer(
	RabbitMqConnectionFactory connectionFactory,
	SendPaymentEmailUseCase sendPaymentEmailUseCase,
	ILogger<EmailConsumer> logger)
	: BaseConsumer<EmailMessage>(connectionFactory, logger)
{
	protected override string QueueName => PaymentSwitchQueues.Queues.EmailSend;

	protected override Task HandleAsync(
		EmailMessage message,
		CancellationToken cancellationToken) =>
		sendPaymentEmailUseCase.ExecuteAsync(message, cancellationToken);
}
