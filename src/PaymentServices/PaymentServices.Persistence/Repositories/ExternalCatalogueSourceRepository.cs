using Microsoft.EntityFrameworkCore;
using PaymentServices.Application.Abstractions;
using PaymentServices.Domain.Entities;

namespace PaymentServices.Persistence.Repositories;

public sealed class ExternalCatalogueSourceRepository(PaymentServicesDbContext dbContext)
	: IExternalCatalogueSourceRepository
{
	private readonly PaymentServicesDbContext _dbContext = dbContext;

	public async Task<ExternalCatalogueSource> GetOrCreateAsync(
		string name,
		Guid countryId,
		CancellationToken cancellationToken = default)
	{
		var source = await _dbContext.ExternalCatalogueSources
			.FirstOrDefaultAsync(x => x.Name == name && x.CountryId == countryId, cancellationToken);

		if (source is not null)
		{
			return source;
		}

		source = new ExternalCatalogueSource
		{
			Id = Guid.NewGuid(),
			Name = name,
			SourceType = "Grpc",
			CountryId = countryId,
			IsActive = true
		};

		_dbContext.ExternalCatalogueSources.Add(source);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return source;
	}
}
