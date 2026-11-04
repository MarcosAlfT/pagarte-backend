using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.UseCases;

namespace PaymentSwitch.Processor.Services;

public sealed class CardRegistrationDispatcherService(IServiceScopeFactory scopes, IClock clock,
	ILogger<CardRegistrationDispatcherService> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				using var scope = scopes.CreateScope();
				var due = await scope.ServiceProvider.GetRequiredService<ICardRegistrationRepository>().GetDueAsync(clock.UtcNow, stoppingToken);
				foreach (var id in due)
				{
					try
					{
						using var operationScope = scopes.CreateScope();
						await operationScope.ServiceProvider.GetRequiredService<ContinueCardRegistrationUseCase>()
							.ExecuteAsync(id, ct: stoppingToken);
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
					catch (Exception) { logger.LogError("Card registration recovery failed for session {SessionId}.", id); }
				}
				var removals = await scope.ServiceProvider.GetRequiredService<ICardDetachmentRepository>().GetDueAsync(clock.UtcNow, stoppingToken);
				foreach (var id in removals)
				{
					try
					{
						using var removalScope = scopes.CreateScope();
						await removalScope.ServiceProvider.GetRequiredService<DetachCreditCardUseCase>().ExecuteAsync(id, stoppingToken);
					}
					catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
					catch (Exception) { logger.LogError("Card detachment recovery failed for card {CardId}.", id); }
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
			catch (Exception) { logger.LogError("Card registration recovery scan failed."); }
		}
	}
}
