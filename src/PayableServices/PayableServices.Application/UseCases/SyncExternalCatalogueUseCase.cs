using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;
using PayableServices.Domain.Enums;

namespace PayableServices.Application.UseCases;

public sealed class SyncExternalCatalogueUseCase(
    ICompanyCatalogueProvider provider, IExternalCatalogueSourceRepository sources,
    IExternalCatalogueItemRepository items, IExternalCatalogueMappingRepository mappings,
    IPayableServiceRepository services, IPaymentRouteRepository routes,
    ICatalogueImportRepository imports, IQuoteConfigurationRepository configuration,
    IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<SyncExternalCatalogueResult> ExecuteAsync(SyncExternalCatalogueCommand request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceName) || request.SourceName.Length > 100 ||
            request.CountryId == Guid.Empty || !string.IsNullOrWhiteSpace(request.Category))
            return Failure("A complete source snapshot and a valid source/country are required.");
        var snapshot = await provider.GetSnapshotAsync(request.SourceName, cancellationToken);
        if (snapshot is null) return Failure("Company catalogue unavailable.");
        if (!Valid(snapshot)) return Failure("Invalid or incomplete catalogue snapshot.");
        var canonical = snapshot with
        {
            Services = snapshot.Services.OrderBy(x => x.Code, StringComparer.Ordinal)
            .Select(x => x with { References = x.References.OrderBy(f => f.FieldKey, StringComparer.Ordinal).ToArray() }).ToArray()
        };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));

        await using var transaction = await unitOfWork.BeginAsync("catalogue", cancellationToken);
        var country = await configuration.GetCountryAsync(request.CountryId, cancellationToken);
        if (country is null || country.Currency != "USD") return Failure("Country must be active and use USD.");
        var source = await sources.GetOrCreateAsync(request.SourceName, request.CountryId, cancellationToken);
        if (snapshot.Version < source.SnapshotVersion) return Failure("Older catalogue snapshot rejected.");
        if (snapshot.Version == source.SnapshotVersion && source.SnapshotFingerprint.Length > 0 && source.SnapshotFingerprint != fingerprint)
            return Failure("A snapshot version cannot be reused with different content.");
        var oldItems = await items.GetBySourceIdAsync(source.Id, cancellationToken);
        var seen = new HashSet<Guid>();
        var routeIds = new List<Guid>();
        var importedRoutes = new List<ImportedRouteDto>();
        var mapped = 0;
        foreach (var item in snapshot.Services)
        {
            var external = oldItems.SingleOrDefault(x => x.ExternalCode == item.Code);
            var mapping = external is null ? null : await mappings.GetByExternalItemIdAsync(external.Id, cancellationToken);
            var service = mapping is null ? null : await services.GetByIdAsync(mapping.PayableServiceId, cancellationToken);
            PaymentRoute? route;
            if (external is not null && (mapping is null || service is null)) return Failure("Existing catalogue mapping is incomplete.");
            if (service is null)
            {
                service = await imports.CreateInactiveServiceAsync(item, request.SourceName, cancellationToken);
                route = (await routes.GetByPayableServiceIdAsync(service.Id, cancellationToken)).Single();
            }
            else
            {
                route = await routes.GetByIdAsync(mapping!.PaymentRouteId, cancellationToken);
                var serviceCountry = await configuration.GetCountryAsync(service.CountryId, cancellationToken);
                if (route is null || route.PayableServiceId != service.Id || route.ExternalSourceCode != request.SourceName ||
                    route.ExternalServiceCode != item.Code || service.Currency != item.Currency || serviceCountry?.Code != item.Country)
                    return Failure("Catalogue mapping is inconsistent.");
                service.Name = item.Name;
                service.Description = item.Description;
                service.SearchKeywords = item.SearchKeywords;
            }
            await imports.ReplaceReferencesAsync(service.Id, item.References, cancellationToken);
            external = await items.UpsertAsync(new ExternalCatalogueItem
            {
                Id = external?.Id ?? Guid.NewGuid(),
                ExternalCatalogueSourceId = source.Id,
                ExternalCode = item.Code,
                ExternalName = item.Name,
                ExternalCategory = item.Category,
                ExternalStatus = "Active",
                IsAvailable = true,
                LastSeenAt = clock.UtcNow
            }, cancellationToken);
            if (mapping is null)
                await mappings.UpsertAsync(new ExternalCatalogueMapping
                {
                    Id = Guid.NewGuid(),
                    ExternalCatalogueItemId = external.Id,
                    PayableServiceId = service.Id,
                    PaymentRouteId = route!.Id,
                    MappingStatus = ExternalCatalogueMappingStatus.ReviewRequired,
                    ReviewReason = "Administrator approval required."
                }, cancellationToken);
            else if (mapping.MappingStatus == ExternalCatalogueMappingStatus.Mapped) mapped++;
            routeIds.Add(route!.Id);
            importedRoutes.Add(new(service.Id, route.Id, item.Code, item.Name));
            seen.Add(external.Id);
        }
        var removed = await items.MarkUnavailableAsync(source.Id, seen, clock.UtcNow, cancellationToken);
        foreach (var item in removed)
        {
            var mapping = await mappings.GetByExternalItemIdAsync(item.Id, cancellationToken);
            if (mapping is null) continue;
            var service = await services.GetByIdAsync(mapping.PayableServiceId, cancellationToken);
            var route = await routes.GetByIdAsync(mapping.PaymentRouteId, cancellationToken);
            if (service is not null) service.IsActive = false;
            if (route is not null) { route.IsActive = false; route.Status = "Unavailable"; }
        }
        source.SnapshotVersion = snapshot.Version;
        source.SnapshotFingerprint = fingerprint;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, snapshot.Services.Count, mapped, snapshot.Services.Count - mapped, removed.Count, null, routeIds, importedRoutes);
    }

    private static bool Valid(CompanyCatalogueSnapshot snapshot) => snapshot.Complete && snapshot.Version > 0 &&
        snapshot.Services is not null && snapshot.Services.Count <= 10000 &&
        snapshot.Services.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() == snapshot.Services.Count &&
        snapshot.Services.All(x => !string.IsNullOrWhiteSpace(x.Code) && x.Code.Length <= 100 &&
            !string.IsNullOrWhiteSpace(x.Name) && x.Name.Length <= 200 && x.Description is { Length: <= 1000 } &&
            !string.IsNullOrWhiteSpace(x.Category) && x.Category.Length <= 200 &&
            !string.IsNullOrWhiteSpace(x.Provider) && x.Provider.Length <= 200 &&
            x.Country is { Length: 2 } && x.Country.All(c => c is >= 'A' and <= 'Z') &&
            x.Currency == "USD" && x.SearchKeywords is { Length: <= 1000 } &&
            x.References is not null && ReferenceValidation.ValidDefinitions(x.References));

    private static SyncExternalCatalogueResult Failure(string error) => new(false, 0, 0, 0, 0, error);
}
