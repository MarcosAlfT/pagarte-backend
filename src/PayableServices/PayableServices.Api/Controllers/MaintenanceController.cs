using Utilities.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayableServices.Api.DTOs;
using PayableServices.Application.Models;
using PayableServices.Application.UseCases;

namespace PayableServices.Api.Controllers;

[ApiController]
[Authorize(Policy = "VerifiedAdmin")]
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
            return BadRequest(ApiResponse.CreateFailure(result.ErrorMessage));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            result.SyncedItems,
            result.MappedItems,
            result.ReviewRequiredItems,
            result.InactivatedItems,
            result.PaymentRouteIds,
            result.Routes
        }, "Catalogue synchronized successfully."));
    }
}
