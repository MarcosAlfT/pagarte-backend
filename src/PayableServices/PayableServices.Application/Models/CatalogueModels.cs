namespace PayableServices.Application.Models;

public sealed record CatalogueQuery(string? Category = null, string? Search = null, string? Provider = null,
    string? Country = null, string? ExternalServiceCode = null, int Page = 1, int PageSize = 20)
{
    public bool IsValid => Page is > 0 and <= 1000000 && PageSize is > 0 and <= 100 &&
        new[] { Category, Search, Provider, ExternalServiceCode }.All(x => x is null ||
            !string.IsNullOrWhiteSpace(x) && x.Length <= 200 && !x.Any(char.IsControl)) &&
        (Country is null || Country.Length == 2 && Country.All(char.IsAsciiLetter));
}

public sealed record CatalogueItemDto(
    Guid Id,
    string Name,
    string Description,
    string Category,
    decimal BaseAmount,
    string Currency);

public sealed record CatalogueResponse(IReadOnlyCollection<CatalogueItemDto> Services);

public sealed record ReferenceDefinitionDto(string FieldKey, string DisplayLabel, string DataType,
    bool IsRequired, int? MinLength, int? MaxLength, string? ValidationRule, int DisplayOrder);
public sealed record ServiceDetailsDto(Guid Id, string Name, string Description, string Provider,
    string Country, string Currency, bool InquiryAvailable, IReadOnlyCollection<ReferenceDefinitionDto> References);
