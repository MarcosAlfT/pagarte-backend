using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface IPaymentOperatorRouteRepository
{
	Task<IReadOnlyList<PaymentOperatorRoute>> PinAsync(IReadOnlyList<PaymentOperatorRoute> configured, CancellationToken ct);
}
