using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Interfaces
{
	public interface IPaymentOperatorRepository
	{
		Task<PaymentOperator?> GetActiveAsync(PaymentOperatorScope scope);
		Task<PaymentOperator?> GetByCodeAsync(string code);
		Task<PaymentOperator> CreateAsync(PaymentOperator paymentOperator);
	}
}
