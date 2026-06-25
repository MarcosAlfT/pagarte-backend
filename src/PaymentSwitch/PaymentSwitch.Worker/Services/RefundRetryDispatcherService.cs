using Infrastructure.RabbitMQ;
using PaymentSwitch.Messaging;
using PaymentSwitch.Worker.Interfaces;

namespace PaymentSwitch.Worker.Services
{
	public sealed class RefundRetryDispatcherService(
		IServiceScopeFactory scopeFactory,
		ILogger<RefundRetryDispatcherService> logger)
		: BackgroundService
	{
		private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
		private const int BatchSize = 25;
		private const int MaxRetries = 3;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			using var timer = new PeriodicTimer(PollInterval);

			while (!stoppingToken.IsCancellationRequested)
			{
				await DispatchDueRefundsAsync(stoppingToken);

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

		private async Task DispatchDueRefundsAsync(CancellationToken cancellationToken)
		{
			try
			{
				using var scope = scopeFactory.CreateScope();
				var paymentStatus =
					scope.ServiceProvider.GetRequiredService<IPaymentStatusRepository>();
				var messagePublisher =
					scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
				var clock = scope.ServiceProvider.GetRequiredService<IClock>();

				var messages = await paymentStatus.GetDueRefundRequestsAsync(
					clock.UtcNow,
					MaxRetries,
					BatchSize);

				foreach (var message in messages)
				{
					await messagePublisher.PublishAsync(
						message,
						PaymentSwitchQueues.Exchanges.Payments,
						PaymentSwitchQueues.Queues.RefundRequest);

					await paymentStatus.MarkRefundRetryDispatchedAsync(
						message.PaymentId);
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Refund retry dispatcher failed.");
			}
		}
	}
}
