using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Abstractions;
using PayableServices.Domain.Entities;

namespace PayableServices.Persistence.Repositories;

public sealed class ExternalCatalogueItemRepository(PayableServicesDbContext dbContext)
	: IExternalCatalogueItemRepository
{
	private readonly PayableServicesDbContext _dbContext = dbContext;

	public async Task<IReadOnlyCollection<ExternalCatalogueItem>> GetBySourceIdAsync(
		Guid sourceId,
		CancellationToken cancellationToken = default)
		=> await _dbContext.ExternalCatalogueItems
			.Where(x => x.ExternalCatalogueSourceId == sourceId)
			.ToListAsync(cancellationToken);

	public async Task<ExternalCatalogueItem> UpsertAsync(
		ExternalCatalogueItem item,
		CancellationToken cancellationToken = default)
	{
		var existing = await _dbContext.ExternalCatalogueItems
			.FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken);

		if (existing is null)
		{
			_dbContext.ExternalCatalogueItems.Add(item);
			await _dbContext.SaveChangesAsync(cancellationToken);
			return item;
		}

		existing.ExternalCatalogueSourceId = item.ExternalCatalogueSourceId;
		existing.ExternalCategory = item.ExternalCategory;
		existing.ExternalSubcategory = item.ExternalSubcategory;
		existing.ExternalName = item.ExternalName;
		existing.ExternalCode = item.ExternalCode;
		existing.ExternalStatus = item.ExternalStatus;
		existing.IsAvailable = item.IsAvailable;
		existing.LastSeenAt = item.LastSeenAt;
		existing.RawReference = item.RawReference;

		_dbContext.ExternalCatalogueItems.Update(existing);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return existing;
	}

	public async Task<IReadOnlyCollection<ExternalCatalogueItem>> MarkUnavailableAsync(
		Guid sourceId,
		IEnumerable<Guid> seenExternalItemIds,
		DateTime utcNow,
		CancellationToken cancellationToken = default)
	{
		var seen = seenExternalItemIds.ToHashSet();
		var items = await _dbContext.ExternalCatalogueItems
			.Where(x => x.ExternalCatalogueSourceId == sourceId && !seen.Contains(x.Id))
			.ToListAsync(cancellationToken);

		foreach (var item in items)
		{
			item.IsAvailable = false;
			item.ExternalStatus = "Inactive";
			item.LastSeenAt = utcNow;
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		return items;
	}
}
