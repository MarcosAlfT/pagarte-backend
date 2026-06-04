using PayableServices.Domain.Entities;

namespace PayableServices.Application.Abstractions;

public interface IQuoteRepository
{
	Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<Quote> CreateAsync(Quote quote, CancellationToken cancellationToken = default);
	Task UpdateAsync(Quote quote, CancellationToken cancellationToken = default);
}
