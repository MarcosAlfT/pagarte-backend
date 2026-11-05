using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayableServices.Application.Abstractions;
using PayableServices.Application.UseCases;

namespace PayableServices.Infrastructure;

public sealed class QuoteSubmissionRecoveryService(
    IServiceScopeFactory scopes, ILogger<QuoteSubmissionRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scan = scopes.CreateScope();
                    var ids = await scan.ServiceProvider.GetRequiredService<IQuoteRepository>().GetRecoverableSubmissionsAsync(
                        scan.ServiceProvider.GetRequiredService<IClock>().UtcNow, stoppingToken);
                    foreach (var id in ids)
                    {
                        try
                        {
                            using var scope = scopes.CreateScope();
                            await scope.ServiceProvider.GetRequiredService<ConfirmQuoteUseCase>().RecoverAsync(id, stoppingToken);
                        }
                        catch (Exception) when (!stoppingToken.IsCancellationRequested)
                        { logger.LogWarning("Quote {QuoteId} recovery deferred; the durable submission remains consumed.", id); }
                    }
                }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                { logger.LogWarning("Quote submission recovery scan unavailable; retrying on the next interval."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
