using Microsoft.AspNetCore.Mvc;
using Utilities.Responses;
using PayableServices.Api.DTOs;
using PayableServices.Application.Models;
using PayableServices.Application.UseCases;

namespace PayableServices.Api.Controllers;

[ApiController]
[Route("api/catalogue")]
public sealed class CatalogueController(GetCatalogueUseCase useCase, GetServiceDetailsUseCase details) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] CatalogueQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (Request.Query.Any(pair => pair.Value.Count != 1 || string.IsNullOrWhiteSpace(pair.Value[0])))
            return BadRequest(ApiResponse.CreateFailure("Invalid catalogue filters or pagination."));
        var query = new CatalogueQuery(request.Category, request.Search, request.Provider, request.Country,
            request.ExternalServiceCode, request.Page, request.PageSize);
        if (!query.IsValid) return BadRequest(ApiResponse.CreateFailure("Invalid catalogue filters or pagination."));
        var result = await useCase.ExecuteAsync(
            query,
            cancellationToken);

        return Ok(ApiResponse<object>.CreateSuccess(result.Services));
    }
    [HttpGet("{serviceId:guid}")]
    public async Task<IActionResult> GetDetailsAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var result = await details.ExecuteAsync(serviceId, cancellationToken);
        return result is null ? NotFound(ApiResponse.CreateFailure("Service not found."))
            : Ok(ApiResponse<object>.CreateSuccess(result));
    }
}
