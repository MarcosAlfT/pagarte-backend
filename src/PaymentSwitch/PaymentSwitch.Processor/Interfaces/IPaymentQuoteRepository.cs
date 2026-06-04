using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Interfaces
{
	public interface IPaymentQuoteRepository
	{
		Task<PaymentQuote?> GetByIdAsync(Guid id);
		Task<PaymentQuote> CreateAsync(PaymentQuote quote);
		Task UpdateAsync(PaymentQuote quote);
	}
}
