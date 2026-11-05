using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface IQuoteSubmissionRepository
{
    Task<QuoteSubmissionOutcome?> GetAsync(Guid sourceQuoteId, CancellationToken cancellationToken);
    void Add(QuoteSubmissionOutcome outcome);
}
