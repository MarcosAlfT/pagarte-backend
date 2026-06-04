using Microsoft.AspNetCore.Mvc;
using Utilities.Responses;
using PayableServices.Api.DTOs;
using PayableServices.Application.Models;
using PayableServices.Application.UseCases;

namespace PayableServices.Api.Controllers;

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
