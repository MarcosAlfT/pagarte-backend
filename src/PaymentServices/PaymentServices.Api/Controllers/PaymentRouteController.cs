using Infrastructure.Responses;
using Microsoft.AspNetCore.Mvc;
using PaymentServices.Api.DTOs;
using PaymentServices.Application.Models;
using PaymentServices.Application.UseCases;

namespace PaymentServices.Api.Controllers;

[ApiController]
[Route("api/payment-routes")]
public sealed class PaymentRouteController(
	ActivatePaymentRouteUseCase activatePaymentRouteUseCase) : ControllerBase
{
	[HttpPost("activate")]
	public async Task<IActionResult> ActivateAsync(
		[FromBody] ActivatePaymentRouteRequest request,
		CancellationToken cancellationToken)
	{
		var result = await activatePaymentRouteUseCase.ExecuteAsync(
			new ActivatePaymentRouteCommand(request.PaymentRouteId),
			cancellationToken);

		if (!result.Success)
		{
			return Ok(ApiResponse.CreateFailure(result.ErrorMessage));
		}

		return Ok(ApiResponse<object>.CreateSuccess(new
		{
			result.PayableServiceId,
			result.ActivePaymentRouteId
		}, "Payment route activated successfully."));
	}
}
