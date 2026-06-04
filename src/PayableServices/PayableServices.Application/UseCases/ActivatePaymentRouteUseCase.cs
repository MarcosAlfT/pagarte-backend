using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;

namespace PayableServices.Application.UseCases;

public sealed class ActivatePaymentRouteUseCase(
	IPaymentRouteRepository paymentRouteRepository)
{
	public async Task<ActivatePaymentRouteResult> ExecuteAsync(
		ActivatePaymentRouteCommand request,
		CancellationToken cancellationToken = default)
	{
		var route = await paymentRouteRepository.GetByIdAsync(
			request.PaymentRouteId,
			cancellationToken);

		if (route is null)
		{
			return new ActivatePaymentRouteResult(false, null, null, "Payment route not found.");
		}

		await paymentRouteRepository.SetActiveAsync(route.Id, cancellationToken);

		return new ActivatePaymentRouteResult(true, route.PayableServiceId, route.Id, null);
	}
}
