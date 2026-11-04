using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;

namespace PayableServices.Application.UseCases;

public sealed class ListQuotesUseCase(IQuoteRepository quotes, IClock clock)
{
    public async Task<QuoteHistoryResponse> ExecuteAsync(
        string clientId,
        QuoteHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        if (!query.IsValid)
        {
            throw new ArgumentException("Invalid quote filters or pagination.", nameof(query));
        }

        var items = await quotes.ListAsync(clientId, query, clock.UtcNow, cancellationToken);
        return new QuoteHistoryResponse(items, query.Page, query.PageSize);
    }
}
