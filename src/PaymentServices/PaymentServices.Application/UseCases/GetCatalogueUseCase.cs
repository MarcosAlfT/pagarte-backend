using PaymentServices.Application.Abstractions;
using PaymentServices.Application.Models;

namespace PaymentServices.Application.UseCases;

public sealed class GetCatalogueUseCase(IPayableServiceRepository payableServiceRepository)
{
	public async Task<CatalogueResponse> ExecuteAsync(
		CatalogueQuery request,
		CancellationToken cancellationToken = default)
	{
		var services = await payableServiceRepository.GetCatalogueAsync(
			request.Category,
			cancellationToken);

		return new CatalogueResponse(services);
	}
}
