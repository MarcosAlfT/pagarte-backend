using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Messaging;
namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public class PaymentRepository(
		PaymentDbContext context,
		IClock clock) : IPaymentRepository
	{
		private readonly PaymentDbContext _context = context;
		private readonly IClock _clock = clock;

		public async Task<Payment?> GetByIdAsync(Guid id)
			=> await _context.Payments
				.Include(p => p.Details)
				.Include(p => p.Service)
				.Include(p => p.CreditCard)
				.FirstOrDefaultAsync(p => p.Id == id);

		public async Task<IEnumerable<Payment>> GetByClientIdAsync(
			string clientId, int page, int pageSize)
			=> await _context.Payments
				.Include(p => p.Details)
				.Include(p => p.Service)
				.Where(p => p.ClientId == clientId)
				.OrderByDescending(p => p.CreatedAt)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

		public async Task<int> GetCountByClientIdAsync(string clientId)
			=> await _context.Payments.CountAsync(p => p.ClientId == clientId);

		public Task<Payment> CreateAsync(Payment payment)
		{
			_context.Payments.Add(payment);
			return Task.FromResult(payment);
		}

		public Task UpdateAsync(Payment payment)
		{
			_context.Payments.Update(payment);
			return Task.CompletedTask;
		}

		public async Task<IEnumerable<Payment>> GetPendingRefundsAsync()
			=> await _context.Payments
				.Where(p => p.Status == PaymentTransactionStatus.Refunding
					&& p.RetryCount < 3
					&& p.NextRetryAt <= _clock.UtcNow)
				.ToListAsync();
	}
}
