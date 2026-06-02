using Microsoft.AspNetCore.Mvc;
using Infrastructure.Responses;
using PaymentServices.Api.DTOs;
using PaymentServices.Application.Models;
using PaymentServices.Application.UseCases;

namespace PaymentServices.Api.Controllers;

[ApiController]
[Route("api/catalogue")]
public sealed class CatalogueController(GetCatalogueUseCase useCase) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> GetAsync(
		[FromQuery] CatalogueQueryRequest request,
		CancellationToken cancellationToken)
	{
		var result = await useCase.ExecuteAsync(
			new CatalogueQuery(request.Category),
			cancellationToken);

		return Ok(ApiResponse<object>.CreateSuccess(result.Services));
	}
}
