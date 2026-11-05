using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure.Repository;

public sealed class QuoteSubmissionRepository(PaymentDbContext db) : IQuoteSubmissionRepository
{
    public Task<QuoteSubmissionOutcome?> GetAsync(Guid sourceQuoteId, CancellationToken cancellationToken) =>
        db.QuoteSubmissionOutcomes.SingleOrDefaultAsync(x => x.SourceQuoteId == sourceQuoteId, cancellationToken);

    public void Add(QuoteSubmissionOutcome outcome) => db.QuoteSubmissionOutcomes.Add(outcome);
}
