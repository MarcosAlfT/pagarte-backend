using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface ICardReviewRepository
{
	Task<IReadOnlyList<CardReviewItem>> ListAsync(int limit, CancellationToken ct);
	Task<CardReviewOperation?> FindAsync(string actor, string key, CancellationToken ct);
	Task<CardReviewOperation?> GetAsync(Guid id, CancellationToken ct);
	void Add(CardReviewOperation operation);
}
