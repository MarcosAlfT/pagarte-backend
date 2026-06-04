namespace PayableServices.Api.DTOs;

public sealed record SyncExternalCatalogueRequest(
	string SourceName,
	Guid CountryId,
	string? Category = null);

public sealed record ActivatePaymentRouteRequest(Guid PaymentRouteId);
