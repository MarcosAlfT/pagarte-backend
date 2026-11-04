using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface IPaymentOperatorRouteConfiguration
{
	IReadOnlyList<PaymentOperatorRoute> GetConfigured();
	void Activate(IReadOnlyList<PaymentOperatorRoute> pinned);
}
