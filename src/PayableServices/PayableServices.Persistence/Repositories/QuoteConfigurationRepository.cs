using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Abstractions;
using PayableServices.Domain.Entities;

namespace PayableServices.Persistence.Repositories;

public sealed class QuoteConfigurationRepository(PayableServicesDbContext dbContext)
	: IQuoteConfigurationRepository
{
	public Task<Country?> GetCountryAsync(
		Guid countryId,
		CancellationToken cancellationToken = default)
		=> dbContext.Countries.FirstOrDefaultAsync(
			country => country.Id == countryId && country.IsActive,
			cancellationToken);

	public Task<AmountComposition?> GetAmountCompositionAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default)
		=> dbContext.AmountCompositions
			.Include(composition => composition.Components)
			.FirstOrDefaultAsync(
				composition => composition.PayableServiceId == payableServiceId,
				cancellationToken);

	public async Task<IReadOnlyCollection<ReferenceFieldDefinition>> GetReferenceFieldsAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default)
		=> await dbContext.ReferenceFieldDefinitions
			.Where(field => field.PayableServiceId == payableServiceId)
			.OrderBy(field => field.DisplayOrder)
			.ToArrayAsync(cancellationToken);
}
