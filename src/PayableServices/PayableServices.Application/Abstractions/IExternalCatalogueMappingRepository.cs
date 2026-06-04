using PayableServices.Domain.Entities;

namespace PayableServices.Application.Abstractions;

public interface IExternalCatalogueMappingRepository
{
	Task<ExternalCatalogueMapping?> GetByExternalItemIdAsync(
		Guid externalCatalogueItemId,
		CancellationToken cancellationToken = default);

	Task<ExternalCatalogueMapping> UpsertAsync(
		ExternalCatalogueMapping mapping,
		CancellationToken cancellationToken = default);
}
