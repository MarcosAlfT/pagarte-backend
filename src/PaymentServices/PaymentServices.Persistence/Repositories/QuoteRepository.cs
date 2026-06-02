using Microsoft.EntityFrameworkCore;
using PaymentServices.Application.Abstractions;
using PaymentServices.Domain.Entities;

namespace PaymentServices.Persistence.Repositories;

public sealed class QuoteRepository(PaymentServicesDbContext dbContext) : IQuoteRepository
{
	private readonly PaymentServicesDbContext _dbContext = dbContext;

	public async Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
		=> await _dbContext.Quotes
			.Include(x => x.Items)
			.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

	public async Task<Quote> CreateAsync(Quote quote, CancellationToken cancellationToken = default)
	{
		_dbContext.Quotes.Add(quote);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return quote;
	}

	public async Task UpdateAsync(Quote quote, CancellationToken cancellationToken = default)
	{
		_dbContext.Quotes.Update(quote);
		await _dbContext.SaveChangesAsync(cancellationToken);
	}
}
