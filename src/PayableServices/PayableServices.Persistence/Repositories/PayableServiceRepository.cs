using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;

namespace PayableServices.Persistence.Repositories;

public sealed class PayableServiceRepository(PayableServicesDbContext dbContext)
    : IPayableServiceRepository
{
    private readonly PayableServicesDbContext _dbContext = dbContext;

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
    public async Task<IReadOnlyCollection<CatalogueItemDto>> SearchAsync(CatalogueQuery filter, CancellationToken cancellationToken)
    {
        var query = from service in _dbContext.PayableServices
                    join category in _dbContext.Categories on service.CategoryId equals category.Id
                    join subcategory in _dbContext.Subcategories on service.SubcategoryId equals subcategory.Id
                    join provider in _dbContext.Providers on service.ProviderId equals provider.Id
                    join country in _dbContext.Countries on service.CountryId equals country.Id
                    where service.IsActive && service.AllowsQuote && service.AllowsPayment && category.IsActive && subcategory.IsActive
                        && country.IsActive && provider.IsActive
                    select new { service, category, provider, country };
        if (filter.Category is not null) query = query.Where(x => x.category.Name == filter.Category);
        if (filter.Provider is not null) query = query.Where(x => x.provider.Name == filter.Provider);
        if (filter.Country is not null) query = query.Where(x => x.country.Code == filter.Country.ToUpper());
        if (filter.ExternalServiceCode is not null) query = query.Where(x => _dbContext.PaymentRoutes.Any(r =>
            r.PayableServiceId == x.service.Id && r.ExternalServiceCode == filter.ExternalServiceCode));
        if (filter.Search is not null)
        {
            var text = filter.Search.ToLower();
            query = query.Where(x => x.service.Name.ToLower().Contains(text) ||
                x.service.Description.ToLower().Contains(text) || x.service.SearchKeywords.ToLower().Contains(text));
        }
        return await query.OrderBy(x => x.service.DisplayOrder).ThenBy(x => x.service.Name).ThenBy(x => x.service.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new CatalogueItemDto(x.service.Id, x.service.Name, x.service.Description,
                x.category.Name, x.service.BaseAmount, x.service.Currency)).ToListAsync(cancellationToken);
    }

    public async Task<ServiceDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var eligible = await SearchByIdAsync(id, cancellationToken);
        if (!eligible) return null;
        var service = await _dbContext.PayableServices.SingleAsync(x => x.Id == id, cancellationToken);
        var provider = await _dbContext.Providers.SingleAsync(x => x.Id == service.ProviderId, cancellationToken);
        var country = await _dbContext.Countries.SingleAsync(x => x.Id == service.CountryId, cancellationToken);
        var fields = await _dbContext.ReferenceFieldDefinitions.Where(x => x.PayableServiceId == id)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.FieldKey).Select(x => new ReferenceDefinitionDto(x.FieldKey,
                x.DisplayLabel, x.DataType, x.IsRequired, x.MinLength, x.MaxLength, x.ValidationRule, x.DisplayOrder)).ToListAsync(cancellationToken);
        return new(id, service.Name, service.Description, provider.Name, country.Code, service.Currency, false, fields);
    }

    private Task<bool> SearchByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (from service in _dbContext.PayableServices
         join category in _dbContext.Categories on service.CategoryId equals category.Id
         join subcategory in _dbContext.Subcategories on service.SubcategoryId equals subcategory.Id
         join provider in _dbContext.Providers on service.ProviderId equals provider.Id
         join country in _dbContext.Countries on service.CountryId equals country.Id
         where service.Id == id && service.IsActive && service.AllowsQuote && service.AllowsPayment &&
             category.IsActive && subcategory.IsActive && provider.IsActive && country.IsActive
         select service.Id).AnyAsync(cancellationToken);
}
