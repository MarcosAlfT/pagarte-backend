using Infrastructure.Responses;
using Microsoft.AspNetCore.Mvc;
using PaymentServices.Api.DTOs;
using PaymentServices.Application.Models;
using PaymentServices.Application.UseCases;

namespace PaymentServices.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public sealed class QuoteController(CreateQuoteUseCase useCase) : ControllerBase
{
	[HttpPost]
	public async Task<IActionResult> CreateAsync(
		[FromBody] CreateQuoteRequest request,
		CancellationToken cancellationToken)
	{
		var result = await useCase.ExecuteAsync(
			new CreateQuoteCommand(request.ClientId, request.ServiceId, request.Currency),
			cancellationToken);

		if (!result.Success)
		{
			return Ok(ApiResponse.CreateFailure(result.ErrorMessage));
		}

		return Ok(ApiResponse<object>.CreateSuccess(
			result.Quote,
			"Quote created successfully."));
	}
}
