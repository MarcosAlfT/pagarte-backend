using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions
{
	public interface IPaymentRequestOutbox
	{
		void AddPaymentRequest(Payment payment, PaymentQuote quote, string clientId);
	}
}
