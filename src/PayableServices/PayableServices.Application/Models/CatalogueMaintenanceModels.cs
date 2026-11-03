namespace PayableServices.Application.Models;

public sealed record SyncExternalCatalogueCommand(
    string SourceName,
    Guid CountryId,
    string? Category = null);

public sealed record SyncExternalCatalogueResult(
    bool Success,
    int SyncedItems,
    int MappedItems,
    int ReviewRequiredItems,
    int InactivatedItems,
    string? ErrorMessage,
    IReadOnlyCollection<Guid>? PaymentRouteIds = null, IReadOnlyCollection<ImportedRouteDto>? Routes = null);

public sealed record ActivatePaymentRouteCommand(Guid PaymentRouteId);

public sealed record ActivatePaymentRouteResult(
    bool Success,
    Guid? PayableServiceId,
    Guid? ActivePaymentRouteId,
    string? ErrorMessage);

public sealed record ImportedRouteDto(Guid ServiceId, Guid PaymentRouteId, string ExternalServiceCode, string Name);
