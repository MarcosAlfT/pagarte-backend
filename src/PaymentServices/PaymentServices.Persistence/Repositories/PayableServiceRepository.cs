using Microsoft.EntityFrameworkCore;
using PaymentServices.Application.Abstractions;
using PaymentServices.Application.Models;
using PaymentServices.Domain.Entities;

namespace PaymentServices.Persistence.Repositories;

public sealed class PayableServiceRepository(PaymentServicesDbContext dbContext)
	: IPayableServiceRepository
{
	private readonly PaymentServicesDbContext _dbContext = dbContext;

	public async Task<PayableService?> GetByIdAsync(
		Guid id,
		CancellationToken cancellationToken = default)
		=> await _dbContext.PayableServices
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

	public async Task<IReadOnlyCollection<CatalogueItemDto>> GetCatalogueAsync(
		string? category = null,
		CancellationToken cancellationToken = default)
	{
		var query =
			from service in _dbContext.PayableServices
			join cat in _dbContext.Categories on service.CategoryId equals cat.Id
			join sub in _dbContext.Subcategories on service.SubcategoryId equals sub.Id
			where service.IsActive
				&& service.AllowsQuote
				&& service.AllowsPayment
				&& cat.IsActive
				&& sub.IsActive
				&& (string.IsNullOrWhiteSpace(category) || cat.Name == category)
			orderby cat.DisplayOrder, sub.DisplayOrder, service.DisplayOrder
			select new CatalogueItemDto(
				service.Id,
				service.Name,
				service.Description,
				cat.Name,
				service.BaseAmount,
				service.Currency);

		return await query.ToListAsync(cancellationToken);
	}
}
