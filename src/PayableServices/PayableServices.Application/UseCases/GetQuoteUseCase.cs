using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;

namespace PayableServices.Application.UseCases;

public sealed class GetQuoteUseCase(IQuoteRepository quoteRepository, IClock clock)
{
	public async Task<QuoteDto?> ExecuteAsync(
		string clientId,
		Guid quoteId,
		CancellationToken cancellationToken = default)
	{
		var quote = await quoteRepository.GetByIdAsync(quoteId, cancellationToken);
		return quote is null || quote.ClientId != clientId ? null : QuoteMapper.Map(quote, clock.UtcNow);
	}
}
