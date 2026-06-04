using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Interfaces
{
	public interface ICreditCardRepository
	{
		Task<IEnumerable<CreditCard>> GetByClientIdAsync(string clientId);
		Task<CreditCard?> GetByIdAsync(Guid id);
		Task<CreditCard> CreateAsync(CreditCard card);
		Task UpdateAsync(CreditCard card);
		Task DeleteAsync(Guid id, string clientId);
	}
}
