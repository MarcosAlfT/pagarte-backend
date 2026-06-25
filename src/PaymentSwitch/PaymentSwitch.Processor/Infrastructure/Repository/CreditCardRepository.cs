using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Messaging;

namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public class CreditCardRepository(
		PaymentDbContext context,
		IClock clock) : ICreditCardRepository
	{
		private readonly PaymentDbContext _context = context;
		private readonly IClock _clock = clock;

		public async Task<IEnumerable<CreditCard>> GetByClientIdAsync(string clientId)
			=> await _context.CreditCards
				.Where(c => c.ClientId == clientId)
				.OrderByDescending(c => c.IsDefault)
				.ThenByDescending(c => c.CreatedAt)
				.ToListAsync();

		public async Task<CreditCard?> GetByIdAsync(Guid id)
			=> await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == id);

		public Task<CreditCard> CreateAsync(CreditCard card)
		{
			_context.CreditCards.Add(card);
			return Task.FromResult(card);
		}

		public Task UpdateAsync(CreditCard card)
		{
			_context.CreditCards.Update(card);
			return Task.CompletedTask;
		}

		public async Task DeleteAsync(Guid id, string clientId)
		{
			var card = await _context.CreditCards
				.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);
			if (card != null)
			{
				card.Delete(_clock.UtcNow);
			}
		}
	}
}
