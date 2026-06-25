using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Application.Abstractions
{
	public interface IPaymentQuotePricingService
	{
		IReadOnlyCollection<PaymentQuotePriceLine> Calculate(
			Service service,
			IReadOnlyCollection<FeeConfiguration> fees,
			string currency);
	}

	public sealed record PaymentQuotePriceLine(
		PaymentDetailType Type,
		string Description,
		decimal Amount,
		string Currency);
}
