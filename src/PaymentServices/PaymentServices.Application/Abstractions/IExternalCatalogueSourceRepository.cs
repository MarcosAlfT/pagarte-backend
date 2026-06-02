using PaymentServices.Domain.Entities;

namespace PaymentServices.Application.Abstractions;

public interface IExternalCatalogueSourceRepository
{
	Task<ExternalCatalogueSource> GetOrCreateAsync(
		string name,
		Guid countryId,
		CancellationToken cancellationToken = default);
}
