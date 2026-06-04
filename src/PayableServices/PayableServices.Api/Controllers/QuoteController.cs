using Utilities.Responses;
using Microsoft.AspNetCore.Mvc;
using PayableServices.Api.DTOs;
using PayableServices.Application.Models;
using PayableServices.Application.UseCases;

namespace PayableServices.Api.Controllers;

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
