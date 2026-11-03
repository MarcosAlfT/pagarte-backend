using System.Text.Json.Serialization;
using PayableServices.Application.Models;

namespace PayableServices.Api.DTOs;

public sealed record CatalogueQueryRequest(string? Category = null, string? Search = null, string? Provider = null,
    string? Country = null, string? ExternalServiceCode = null, int Page = 1, int PageSize = 20);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateQuoteRequest(Guid ServiceId = default, string? Currency = null,
    IReadOnlyDictionary<string, string>? ReferenceValues = null, Guid? InquiryId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateInquiryRequest(Guid ServiceId, IReadOnlyCollection<ReferenceValue> References);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmQuoteRequest(Guid CreditCardId);
