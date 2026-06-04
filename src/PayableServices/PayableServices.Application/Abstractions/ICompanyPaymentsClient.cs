using PayableServices.Application.Models;

namespace PayableServices.Application.Abstractions;

public interface ICompanyPaymentsClient
{
	Task<CatalogueResponse> GetCatalogueAsync(
		string? category = null,
		CancellationToken cancellationToken = default);

	Task<CatalogueItemDto?> GetServiceAsync(
		Guid serviceId,
		CancellationToken cancellationToken = default);
}
