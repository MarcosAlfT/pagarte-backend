using PaymentServices.Domain.Entities;

namespace PaymentServices.Application.Abstractions;

public interface IExternalCatalogueMappingRepository
{
	Task<ExternalCatalogueMapping?> GetByExternalItemIdAsync(
		Guid externalCatalogueItemId,
		CancellationToken cancellationToken = default);

	Task<ExternalCatalogueMapping> UpsertAsync(
		ExternalCatalogueMapping mapping,
		CancellationToken cancellationToken = default);
}
