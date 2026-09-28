using PayableServices.Domain.Entities;

namespace PayableServices.Application.Abstractions;

public interface IQuoteConfigurationRepository
{
	Task<Country?> GetCountryAsync(
		Guid countryId,
		CancellationToken cancellationToken = default);

	Task<AmountComposition?> GetAmountCompositionAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyCollection<ReferenceFieldDefinition>> GetReferenceFieldsAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default);
}
