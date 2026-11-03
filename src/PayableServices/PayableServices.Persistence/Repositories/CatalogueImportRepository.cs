using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;

namespace PayableServices.Persistence.Repositories;

public sealed class CatalogueImportRepository(PayableServicesDbContext db) : ICatalogueImportRepository
{
    public async Task<PayableService> CreateInactiveServiceAsync(CompanyCatalogueItem item, string source, CancellationToken cancellationToken)
    {
        var country = db.Countries.Local.FirstOrDefault(x => x.Code == item.Country) ??
            await db.Countries.SingleOrDefaultAsync(x => x.Code == item.Country, cancellationToken);
        if (country is null)
        {
            country = new Country { Id = Guid.NewGuid(), Code = item.Country, Name = item.Country, Currency = item.Currency, IsActive = true };
            db.Countries.Add(country);
        }
        if (country.Currency != item.Currency) throw new InvalidOperationException("Country currency mismatch.");
        var category = db.Categories.Local.FirstOrDefault(x => x.CountryId == country.Id && x.Name == item.Category) ??
            await db.Categories.FirstOrDefaultAsync(x => x.CountryId == country.Id && x.Name == item.Category, cancellationToken);
        if (category is null)
        {
            category = new Category { Id = Guid.NewGuid(), CountryId = country.Id, Name = item.Category, IsActive = true };
            db.Categories.Add(category);
        }
        var subcategory = db.Subcategories.Local.FirstOrDefault(x => x.CategoryId == category.Id) ??
            await db.Subcategories.FirstOrDefaultAsync(x => x.CategoryId == category.Id, cancellationToken);
        if (subcategory is null)
        {
            subcategory = new Subcategory { Id = Guid.NewGuid(), CategoryId = category.Id, Name = item.Category, IsActive = true };
            db.Subcategories.Add(subcategory);
        }
        var provider = db.Providers.Local.FirstOrDefault(x => x.CountryId == country.Id && x.Name == item.Provider) ??
            await db.Providers.FirstOrDefaultAsync(x => x.CountryId == country.Id && x.Name == item.Provider, cancellationToken);
        if (provider is null)
        {
            provider = new Provider { Id = Guid.NewGuid(), CountryId = country.Id, Name = item.Provider, IsActive = true };
            db.Providers.Add(provider);
        }
        var service = new PayableService
        {
            Id = Guid.NewGuid(),
            CountryId = country.Id,
            CategoryId = category.Id,
            SubcategoryId = subcategory.Id,
            ProviderId = provider.Id,
            Name = item.Name,
            Description = item.Description,
            Currency = item.Currency,
            SearchKeywords = item.SearchKeywords,
            Status = "ReviewRequired",
            RequiresInquiry = true,
            AllowsQuote = true,
            AllowsPayment = true
        };
        db.PayableServices.Add(service);
        db.PaymentRoutes.Add(new PaymentRoute
        {
            Id = Guid.NewGuid(),
            PayableServiceId = service.Id,
            ProviderId = provider.Id,
            RouteType = "DirectProviderApi",
            ExternalSourceCode = source,
            ExternalServiceCode = item.Code,
            InquiryProvider = source,
            Status = "ReviewRequired"
        });
        return service;
    }

    public async Task ReplaceReferencesAsync(Guid serviceId, IReadOnlyCollection<ReferenceFieldDefinition> fields, CancellationToken cancellationToken)
    {
        db.ReferenceFieldDefinitions.RemoveRange(await db.ReferenceFieldDefinitions.Where(x => x.PayableServiceId == serviceId).ToListAsync(cancellationToken));
        foreach (var field in fields)
            db.ReferenceFieldDefinitions.Add(new ReferenceFieldDefinition
            {
                Id = Guid.NewGuid(),
                PayableServiceId = serviceId,
                FieldKey = field.FieldKey,
                DisplayLabel = field.DisplayLabel,
                DataType = field.DataType,
                IsRequired = field.IsRequired,
                MinLength = field.MinLength,
                MaxLength = field.MaxLength,
                ValidationRule = field.ValidationRule,
                DisplayOrder = field.DisplayOrder
            });
    }

    public Task<ExternalCatalogueMapping?> GetRouteMappingAsync(Guid routeId, CancellationToken cancellationToken) =>
        db.ExternalCatalogueMappings.SingleOrDefaultAsync(x => x.PaymentRouteId == routeId, cancellationToken);
    public Task<string?> GetSourceNameAsync(Guid sourceId, CancellationToken cancellationToken) =>
        db.ExternalCatalogueSources.Where(x => x.Id == sourceId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken);
    public Task<ExternalCatalogueItem?> GetItemAsync(Guid id, CancellationToken cancellationToken) =>
        db.ExternalCatalogueItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}
