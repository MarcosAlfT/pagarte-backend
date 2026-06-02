using PaymentServices.Application.Abstractions;
using PaymentServices.Application.Models;
using PaymentServices.Domain.Entities;
using PaymentServices.Domain.Enums;

namespace PaymentServices.Application.UseCases;

public sealed class CreateQuoteUseCase(
	IPayableServiceRepository payableServiceRepository,
	IQuoteRepository quoteRepository,
	IClock clock)
{
	public async Task<CreateQuoteResult> ExecuteAsync(
		CreateQuoteCommand request,
		CancellationToken cancellationToken = default)
	{
		var service = await payableServiceRepository.GetByIdAsync(
			request.ServiceId,
			cancellationToken);

		if (service is null)
		{
			return new CreateQuoteResult(false, null, "Service not found.");
		}

		if (!service.IsActive || !service.AllowsQuote)
		{
			return new CreateQuoteResult(false, null, "Service is not available for quoting.");
		}

		if (!string.Equals(service.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
		{
			return new CreateQuoteResult(false, null, "Currency is not valid for this service.");
		}

		var quote = new Quote
		{
			Id = Guid.NewGuid(),
			ClientId = request.ClientId,
			ServiceId = service.Id,
			Currency = request.Currency.ToUpperInvariant(),
			Status = QuoteStatus.Unpaid,
			CreatedAt = clock.UtcNow,
			ExpiresAt = clock.UtcNow.AddMinutes(60),
			TotalAmount = service.BaseAmount,
			ServiceName = service.Name
		};

		quote.Items.Add(new QuoteItem
		{
			Id = Guid.NewGuid(),
			QuoteId = quote.Id,
			PayableServiceId = service.Id,
			Description = service.Name,
			Amount = service.BaseAmount,
			Currency = quote.Currency
		});

		await quoteRepository.CreateAsync(quote, cancellationToken);

		return new CreateQuoteResult(true, MapQuote(quote), null);
	}

	private static QuoteDto MapQuote(Quote quote)
		=> new(
			quote.Id,
			quote.ClientId,
			quote.ServiceId,
			quote.ServiceName,
			quote.Currency,
			quote.Status.ToString(),
			quote.TotalAmount,
			quote.CreatedAt,
			quote.ExpiresAt,
			quote.Items.Select(item => new QuoteLineDto(
				item.Id,
				item.Description,
				item.Amount,
				item.Currency)).ToArray());
}
