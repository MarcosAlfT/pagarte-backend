namespace PaymentServices.Api.DTOs;

public sealed record CatalogueQueryRequest(string? Category = null);

public sealed record CreateQuoteRequest(
	string ClientId,
	Guid ServiceId,
	string Currency);
