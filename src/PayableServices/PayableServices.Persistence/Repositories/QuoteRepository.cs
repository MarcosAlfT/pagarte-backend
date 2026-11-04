using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Abstractions;
using PayableServices.Domain.Entities;
using PayableServices.Application.Models;
using PayableServices.Domain.Enums;

namespace PayableServices.Persistence.Repositories;

public sealed class QuoteRepository(PayableServicesDbContext dbContext) : IQuoteRepository
{
    private readonly PayableServicesDbContext _dbContext = dbContext;

    public async Task<IReadOnlyCollection<QuoteSummaryDto>> ListAsync(
        string clientId,
        QuoteHistoryQuery filter,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Quotes.AsNoTracking()
            .Where(quote => quote.ClientId == clientId)
            .Select(quote => new
            {
                Quote = quote,
                Status = quote.Status == QuoteStatus.Unpaid && quote.PaymentId == null && quote.ExpiresAt <= utcNow
                    ? QuoteStatus.Expired
                    : quote.Status
            });

        if (filter.Status is { } status) query = query.Where(item => item.Status == status);
        if (filter.ServiceId is { } serviceId) query = query.Where(item => item.Quote.ServiceId == serviceId);
        if (filter.CreatedFrom is { } from)
        {
            var utcFrom = from.UtcDateTime;
            query = query.Where(item => item.Quote.CreatedAt >= utcFrom);
        }
        if (filter.CreatedTo is { } to)
        {
            var utcTo = to.UtcDateTime;
            query = query.Where(item => item.Quote.CreatedAt <= utcTo);
        }

        return await query.OrderByDescending(item => item.Quote.CreatedAt)
            .ThenByDescending(item => item.Quote.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(item => new QuoteSummaryDto(
                item.Quote.Id,
                item.Quote.ServiceId,
                item.Quote.ServiceName,
                item.Status.ToString(),
                item.Quote.TotalAmount,
                item.Quote.TotalMinorUnits,
                item.Quote.Currency,
                item.Quote.CreatedAt,
                item.Quote.ExpiresAt,
                item.Quote.PaymentId,
                item.Quote.InquiryId,
                item.Quote.ReferenceValues.OrderBy(reference => reference.Key)
                    .Select(reference => new QuoteReferenceValueDto(reference.Key, reference.Value)).ToArray()))
            .ToListAsync(cancellationToken);
    }

    public async Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Quotes
            .Include(x => x.Items)
            .Include(x => x.ReferenceValues)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Quote> CreateAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        _dbContext.Quotes.Add(quote);

        return Task.FromResult(quote);
    }

    public Task UpdateAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        _dbContext.Quotes.Update(quote);
        return Task.CompletedTask;
    }
}
