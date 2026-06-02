using Microsoft.EntityFrameworkCore;
using PaymentServices.Application.Abstractions;
using PaymentServices.Domain.Entities;

namespace PaymentServices.Persistence.Repositories;

public sealed class PaymentRouteRepository(PaymentServicesDbContext dbContext)
	: IPaymentRouteRepository
{
	private readonly PaymentServicesDbContext _dbContext = dbContext;

	public Task<PaymentRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		=> _dbContext.PaymentRoutes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

	public Task<PaymentRoute?> GetActiveByPayableServiceIdAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default)
		=> _dbContext.PaymentRoutes.FirstOrDefaultAsync(
			x => x.PayableServiceId == payableServiceId && x.IsActive,
			cancellationToken);

	public async Task<IReadOnlyCollection<PaymentRoute>> GetByPayableServiceIdAsync(
		Guid payableServiceId,
		CancellationToken cancellationToken = default)
		=> await _dbContext.PaymentRoutes
			.Where(x => x.PayableServiceId == payableServiceId)
			.ToListAsync(cancellationToken);

	public async Task SetActiveAsync(Guid paymentRouteId, CancellationToken cancellationToken = default)
	{
		var route = await _dbContext.PaymentRoutes.FirstOrDefaultAsync(
			x => x.Id == paymentRouteId,
			cancellationToken);

		if (route is null)
		{
			return;
		}

		var routes = await _dbContext.PaymentRoutes
			.Where(x => x.PayableServiceId == route.PayableServiceId)
			.ToListAsync(cancellationToken);

		foreach (var candidate in routes)
		{
			candidate.IsActive = candidate.Id == route.Id;
			candidate.Status = candidate.Id == route.Id ? "Active" : "Inactive";
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
	}
}
