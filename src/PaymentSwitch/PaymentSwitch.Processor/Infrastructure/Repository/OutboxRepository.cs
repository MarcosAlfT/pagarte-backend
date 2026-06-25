using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public sealed class OutboxRepository(PaymentDbContext dbContext) : IOutboxRepository
	{
		private readonly PaymentDbContext _dbContext = dbContext;

		public async Task<IReadOnlyCollection<OutboxMessage>> GetPendingAsync(
			DateTime utcNow,
			int batchSize,
			CancellationToken cancellationToken = default)
			=> await _dbContext.OutboxMessages
				.Where(m => m.PublishedAt == null && m.NextAttemptAt <= utcNow)
				.OrderBy(m => m.CreatedAt)
				.Take(batchSize)
				.ToListAsync(cancellationToken);

		public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
			=> await _dbContext.SaveChangesAsync(cancellationToken);
	}
}
