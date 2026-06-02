using Infrastructure.Responses;
using Microsoft.AspNetCore.Mvc;
using PaymentServices.Api.DTOs;
using PaymentServices.Application.Models;
using PaymentServices.Application.UseCases;

namespace PaymentServices.Api.Controllers;

[ApiController]
[Route("api/catalogue/maintenance")]
public sealed class MaintenanceController(
	SyncExternalCatalogueUseCase syncExternalCatalogueUseCase) : ControllerBase
{
	[HttpPost("sync")]
	public async Task<IActionResult> SyncAsync(
		[FromBody] SyncExternalCatalogueRequest request,
		CancellationToken cancellationToken)
	{
		var result = await syncExternalCatalogueUseCase.ExecuteAsync(
			new SyncExternalCatalogueCommand(
				request.SourceName,
				request.CountryId,
				request.Category),
			cancellationToken);

		if (!result.Success)
		{
			return Ok(ApiResponse.CreateFailure(result.ErrorMessage));
		}

		return Ok(ApiResponse<object>.CreateSuccess(new
		{
			result.SyncedItems,
			result.MappedItems,
			result.ReviewRequiredItems,
			result.InactivatedItems
		}, "Catalogue synchronized successfully."));
	}
}
