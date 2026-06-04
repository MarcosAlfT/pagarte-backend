using PayableServices.Domain.Entities;
using PayableServices.Application.Models;

namespace PayableServices.Application.Abstractions;

public interface IPayableServiceRepository
{
	Task<PayableService?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<IReadOnlyCollection<CatalogueItemDto>> GetCatalogueAsync(
		string? category = null,
		CancellationToken cancellationToken = default);
}
