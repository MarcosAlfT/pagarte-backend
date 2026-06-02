using PaymentServices.Domain.Entities;
using PaymentServices.Application.Models;

namespace PaymentServices.Application.Abstractions;

public interface IPayableServiceRepository
{
	Task<PayableService?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<IReadOnlyCollection<CatalogueItemDto>> GetCatalogueAsync(
		string? category = null,
		CancellationToken cancellationToken = default);
}
