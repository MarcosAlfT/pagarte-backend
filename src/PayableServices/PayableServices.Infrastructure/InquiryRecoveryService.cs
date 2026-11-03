using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PayableServices.Application.Abstractions;
using PayableServices.Application.UseCases;

namespace PayableServices.Infrastructure;

public sealed class InquiryRecoveryService(IServiceScopeFactory scopes, ILogger<InquiryRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scan = scopes.CreateScope();
                var ids = await scan.ServiceProvider.GetRequiredService<IInquiryRepository>().GetRecoverableAsync(
                    scan.ServiceProvider.GetRequiredService<IClock>().UtcNow, stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<CreateInquiryUseCase>().RecoverAsync(id, stoppingToken);
                    }
                    catch (Exception) when (!stoppingToken.IsCancellationRequested)
                    { logger.LogWarning("Inquiry {InquiryId} recovery deferred; durable lease permits another attempt.", id); }
                }
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning("Inquiry recovery scan unavailable; retrying on the next interval."); }
        }
    }
}
