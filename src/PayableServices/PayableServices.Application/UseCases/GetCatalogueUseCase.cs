using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;

namespace PayableServices.Application.UseCases;

public sealed class GetCatalogueUseCase(IPayableServiceRepository payableServiceRepository)
{
    public async Task<CatalogueResponse> ExecuteAsync(
        CatalogueQuery request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsValid) throw new ArgumentException("Invalid catalogue filters or pagination.");
        var services = await payableServiceRepository.SearchAsync(request, cancellationToken);

        return new CatalogueResponse(services);
    }
}
