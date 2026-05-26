using Microsoft.EntityFrameworkCore;
using Pagarte.Services.Infrastructure;
using Shared.RabbitMQ;

namespace Pagarte.Services.Services
{
	public class OutboxPublisherService(
		IServiceScopeFactory scopeFactory,
		ILogger<OutboxPublisherService> logger) : BackgroundService
	{
		private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
		private const int BatchSize = 25;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			using var timer = new PeriodicTimer(PollInterval);

			while (!stoppingToken.IsCancellationRequested)
			{
				await PublishPendingMessagesAsync(stoppingToken);

				try
				{
					await timer.WaitForNextTickAsync(stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					break;
				}
			}
		}

		private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
		{
			try
			{
				using var scope = scopeFactory.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<PagarteDbContext>();
				var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
				var utcNow = DateTime.UtcNow;

				var messages = await db.OutboxMessages
					.Where(m => m.PublishedAt == null && m.NextAttemptAt <= utcNow)
					.OrderBy(m => m.CreatedAt)
					.Take(BatchSize)
					.ToListAsync(cancellationToken);

				foreach (var message in messages)
				{
					try
					{
						await publisher.PublishJsonAsync(
							message.Payload,
							message.Exchange,
							message.RoutingKey);

						message.MarkPublished();
					}
					catch (Exception ex)
					{
						message.MarkFailed(ex.Message);
						logger.LogWarning(ex,
							"Failed to publish outbox message {OutboxMessageId}.",
							message.Id);
					}
				}

				if (messages.Count > 0)
				{
					await db.SaveChangesAsync(cancellationToken);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Outbox publisher failed while processing pending messages.");
			}
		}
	}
}
