using PayableServices.Domain.Entities;

namespace PayableServices.Application.Models;

public sealed record CompanyCatalogueItem(string Code, string Name, string Description,
    string Category, string Provider, string Country, string Currency,
    string SearchKeywords, IReadOnlyCollection<ReferenceFieldDefinition> References);
public sealed record CompanyCatalogueSnapshot(long Version, bool Complete,
    IReadOnlyCollection<CompanyCatalogueItem> Services);
