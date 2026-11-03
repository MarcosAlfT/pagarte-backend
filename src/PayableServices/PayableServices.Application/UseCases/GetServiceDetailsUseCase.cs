using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;

namespace PayableServices.Application.UseCases;

public sealed class GetServiceDetailsUseCase(IPayableServiceRepository services,
    IPaymentRouteRepository routes, IAmountInquiryProvider provider)
{
    public async Task<ServiceDetailsDto?> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var details = await services.GetDetailsAsync(id, cancellationToken);
        if (details is null) return null;
        var route = await routes.GetActiveByPayableServiceIdAsync(id, cancellationToken);
        return details with { InquiryAvailable = route?.InquiryProvider is { } name && provider.IsConfigured(name) };
    }
}
