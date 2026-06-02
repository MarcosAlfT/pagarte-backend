using PaymentServices.Domain.Entities;

namespace PaymentServices.Application.Abstractions;

public interface IPaymentRouteRepository
{
	Task<PaymentRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<PaymentRoute?> GetActiveByPayableServiceIdAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyCollection<PaymentRoute>> GetByPayableServiceIdAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default);

	Task SetActiveAsync(Guid paymentRouteId, CancellationToken cancellationToken = default);
}
