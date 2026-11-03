using PayableServices.Application.Models;
using PayableServices.Domain.Entities;

namespace PayableServices.Application.Abstractions;

public interface ICatalogueImportRepository
{
    Task<PayableService> CreateInactiveServiceAsync(CompanyCatalogueItem item, string source,
        CancellationToken cancellationToken);
    Task ReplaceReferencesAsync(Guid serviceId, IReadOnlyCollection<ReferenceFieldDefinition> fields,
        CancellationToken cancellationToken);
    Task<ExternalCatalogueMapping?> GetRouteMappingAsync(Guid routeId, CancellationToken cancellationToken);
    Task<string?> GetSourceNameAsync(Guid sourceId, CancellationToken cancellationToken);
    Task<ExternalCatalogueItem?> GetItemAsync(Guid id, CancellationToken cancellationToken);
}
