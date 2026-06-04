using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Abstractions;
using PayableServices.Domain.Entities;

namespace PayableServices.Persistence.Repositories;

public sealed class ExternalCatalogueMappingRepository(PayableServicesDbContext dbContext)
	: IExternalCatalogueMappingRepository
{
	private readonly PayableServicesDbContext _dbContext = dbContext;

	public Task<ExternalCatalogueMapping?> GetByExternalItemIdAsync(
		Guid externalCatalogueItemId,
		CancellationToken cancellationToken = default)
		=> _dbContext.ExternalCatalogueMappings
			.FirstOrDefaultAsync(x => x.ExternalCatalogueItemId == externalCatalogueItemId, cancellationToken);

	public async Task<ExternalCatalogueMapping> UpsertAsync(
		ExternalCatalogueMapping mapping,
		CancellationToken cancellationToken = default)
	{
		var existing = await _dbContext.ExternalCatalogueMappings
			.FirstOrDefaultAsync(x => x.ExternalCatalogueItemId == mapping.ExternalCatalogueItemId, cancellationToken);

		if (existing is null)
		{
			_dbContext.ExternalCatalogueMappings.Add(mapping);
			await _dbContext.SaveChangesAsync(cancellationToken);
			return mapping;
		}

		existing.PayableServiceId = mapping.PayableServiceId;
		existing.PaymentRouteId = mapping.PaymentRouteId;
		existing.MappingStatus = mapping.MappingStatus;
		existing.ReviewReason = mapping.ReviewReason;

		_dbContext.ExternalCatalogueMappings.Update(existing);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return existing;
	}
}
