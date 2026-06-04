using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace PaymentSwitch.Processor.Infrastructure.Repository
{
	public class CreditCardRepository(PaymentDbContext context) : ICreditCardRepository
	{
		private readonly PaymentDbContext _context = context;

		public async Task<IEnumerable<CreditCard>> GetByClientIdAsync(string clientId)
			=> await _context.CreditCards
				.Where(c => c.ClientId == clientId)
				.OrderByDescending(c => c.IsDefault)
				.ThenByDescending(c => c.CreatedAt)
				.ToListAsync();

		public async Task<CreditCard?> GetByIdAsync(Guid id)
			=> await _context.CreditCards.FirstOrDefaultAsync(c => c.Id == id);

		public async Task<CreditCard> CreateAsync(CreditCard card)
		{
			_context.CreditCards.Add(card);
			await _context.SaveChangesAsync();
			return card;
		}

		public async Task UpdateAsync(CreditCard card)
		{
			_context.CreditCards.Update(card);
			await _context.SaveChangesAsync();
		}

		public async Task DeleteAsync(Guid id, string clientId)
		{
			var card = await _context.CreditCards
				.FirstOrDefaultAsync(c => c.Id == id && c.ClientId == clientId);
			if (card != null)
			{
				card.Delete();
				await _context.SaveChangesAsync();
			}
		}
	}
}
