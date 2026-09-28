namespace PayableServices.Api.DTOs;

public sealed record CatalogueQueryRequest(string? Category = null);

public sealed record CreateQuoteRequest(
	Guid ServiceId,
	string Currency,
	IReadOnlyDictionary<string, string>? ReferenceValues = null);

public sealed record ConfirmQuoteRequest(Guid CreditCardId);
