using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Services;

public sealed class PaymentSagaDispatcherService(
	IServiceScopeFactory scopeFactory,
	IOptions<PaymentExecutionOptions> options,
	ILogger<PaymentSagaDispatcherService> logger,
	IClock clock) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var interval = TimeSpan.FromSeconds(
			Math.Max(1, options.Value.DispatcherIntervalSeconds));
		using var timer = new PeriodicTimer(interval);

		do
		{
			await DispatchAsync(stoppingToken);
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}

	private async Task DispatchAsync(CancellationToken cancellationToken)
	{
		try
		{
			Guid[] paymentIds;
			using (var queryScope = scopeFactory.CreateScope())
			{
				var repository = queryScope.ServiceProvider
					.GetRequiredService<IPaymentRepository>();
				paymentIds = (await repository.GetDueAsync(
					clock.UtcNow,
					options.Value.DispatcherBatchSize,
					cancellationToken))
					.Select(payment => payment.Id)
					.ToArray();
			}

			foreach (var paymentId in paymentIds)
			{
				using var executionScope = scopeFactory.CreateScope();
				var useCase = executionScope.ServiceProvider
					.GetRequiredService<ContinuePaymentExecutionUseCase>();
				try
				{
					await useCase.ExecuteAsync(paymentId, cancellationToken);
				}
				catch (DbUpdateConcurrencyException)
				{
					logger.LogDebug(
						"Payment {PaymentId} was claimed by another dispatcher.",
						paymentId);
				}
				catch (Exception exception)
				{
					logger.LogError(
						exception,
						"Failed to continue payment saga {PaymentId}.",
						paymentId);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Payment saga dispatch cycle failed.");
		}
	}
}
