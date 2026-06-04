namespace PayableServices.Application.Models;

public sealed record CatalogueQuery(string? Category = null);

public sealed record CatalogueItemDto(
	Guid Id,
	string Name,
	string Description,
	string Category,
	decimal BaseAmount,
	string Currency);

public sealed record CatalogueResponse(IReadOnlyCollection<CatalogueItemDto> Services);
