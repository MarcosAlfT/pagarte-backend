using Infrastructure.RabbitMQ;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class PublishPendingOutboxMessagesUseCase(
		IOutboxRepository outboxRepository,
		IMessagePublisher messagePublisher,
		IClock clock,
		ILogger<PublishPendingOutboxMessagesUseCase> logger)
	{
		private const int BatchSize = 25;

		private readonly IOutboxRepository _outboxRepository = outboxRepository;
		private readonly IMessagePublisher _messagePublisher = messagePublisher;
		private readonly IClock _clock = clock;
		private readonly ILogger<PublishPendingOutboxMessagesUseCase> _logger = logger;

		public async Task ExecuteAsync(CancellationToken cancellationToken = default)
		{
			var messages = await _outboxRepository.GetPendingAsync(
				_clock.UtcNow,
				BatchSize,
				cancellationToken);

			foreach (var message in messages)
			{
				try
				{
					await _messagePublisher.PublishJsonAsync(
						message.Payload,
						message.Exchange,
						message.RoutingKey);

					message.MarkPublished(_clock.UtcNow);
				}
				catch (Exception ex)
				{
					message.MarkFailed(ex.Message, _clock.UtcNow);
					_logger.LogWarning(
						ex,
						"Failed to publish outbox message {OutboxMessageId}.",
						message.Id);
				}
			}

			if (messages.Count > 0)
			{
				await _outboxRepository.SaveChangesAsync(cancellationToken);
			}
		}
	}
}
