using PayableServices.Domain.Entities;

namespace PayableServices.Application.Abstractions;

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
