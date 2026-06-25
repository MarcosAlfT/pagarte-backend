using PaymentSwitch.Processor.Application.UseCases;

namespace PaymentSwitch.Processor.Services
{
	public class OutboxPublisherService(
		IServiceScopeFactory scopeFactory,
		ILogger<OutboxPublisherService> logger) : BackgroundService
	{
		private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

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
				var useCase = scope.ServiceProvider
					.GetRequiredService<PublishPendingOutboxMessagesUseCase>();
				await useCase.ExecuteAsync(cancellationToken);
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
