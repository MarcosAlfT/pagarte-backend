using PaymentSwitch.Processor.Application.Abstractions;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class PinPaymentOperatorRoutesUseCase(IPaymentOperatorRouteConfiguration configuration, IPaymentOperatorRouteRepository routes)
{
	public async Task ExecuteAsync(CancellationToken ct = default)
	{
		var pinned = await routes.PinAsync(configuration.GetConfigured(), ct);
		configuration.Activate(pinned);
	}
}
