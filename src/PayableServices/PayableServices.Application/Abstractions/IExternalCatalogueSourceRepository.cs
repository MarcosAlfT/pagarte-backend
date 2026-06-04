using PayableServices.Domain.Entities;

namespace PayableServices.Application.Abstractions;

public interface IExternalCatalogueSourceRepository
{
	Task<ExternalCatalogueSource> GetOrCreateAsync(
		string name,
		Guid countryId,
		CancellationToken cancellationToken = default);
}
