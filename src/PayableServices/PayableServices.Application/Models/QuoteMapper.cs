using PayableServices.Domain.Entities;

namespace PayableServices.Application.Models;

public static class QuoteMapper
{
	public static QuoteDto Map(Quote quote) =>
		new(
			quote.Id,
			quote.ClientId,
			quote.ServiceId,
			quote.ServiceName,
			quote.CountryId,
			quote.CountryCode,
			quote.Currency,
			quote.CurrencyScale,
			quote.RoundingMode,
			quote.Status.ToString(),
			quote.TotalAmount,
			quote.TotalMinorUnits,
			quote.CreatedAt,
			quote.ExpiresAt,
			quote.PaymentId,
			quote.Items
				.OrderBy(item => item.DisplayOrder)
				.Select(item => new QuoteLineDto(
					item.Id,
					item.ComponentType,
					item.Description,
					item.Source,
					item.IsVisibleToClient,
					item.DisplayOrder,
					item.Amount,
					item.MinorUnits,
					item.Currency))
				.ToArray(),
			quote.ReferenceValues
				.OrderBy(reference => reference.Key, StringComparer.Ordinal)
				.Select(reference => new QuoteReferenceValueDto(reference.Key, reference.Value))
				.ToArray());
}
