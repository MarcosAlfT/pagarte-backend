using PaymentServices.Domain.Entities;

namespace PaymentServices.Application.Abstractions;

public interface IExternalCatalogueItemRepository
{
	Task<IReadOnlyCollection<ExternalCatalogueItem>> GetBySourceIdAsync(
		Guid sourceId,
		CancellationToken cancellationToken = default);

	Task<ExternalCatalogueItem> UpsertAsync(
		ExternalCatalogueItem item,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyCollection<ExternalCatalogueItem>> MarkUnavailableAsync(
		Guid sourceId,
		IEnumerable<Guid> seenExternalItemIds,
		DateTime utcNow,
		CancellationToken cancellationToken = default);
}
