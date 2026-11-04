using PayableServices.Domain.Entities;
using PayableServices.Application.Models;

namespace PayableServices.Application.Abstractions;

public interface IQuoteRepository
{
	Task<IReadOnlyCollection<QuoteSummaryDto>> ListAsync(
		string clientId, QuoteHistoryQuery query, DateTime utcNow, CancellationToken cancellationToken = default);
	Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Quote> CreateAsync(Quote quote, CancellationToken cancellationToken = default);
	Task UpdateAsync(Quote quote, CancellationToken cancellationToken = default);
}
