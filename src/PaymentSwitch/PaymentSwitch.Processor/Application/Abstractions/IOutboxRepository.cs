using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions
{
	public interface IOutboxRepository
	{
		Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(
			DateTime utcNow,
			int batchSize,
			CancellationToken cancellationToken = default);

		Task SaveChangesAsync(CancellationToken cancellationToken = default);
	}
}
