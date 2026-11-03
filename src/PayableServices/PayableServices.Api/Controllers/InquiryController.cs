using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayableServices.Api.DTOs;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Application.UseCases;
using Utilities.Responses;

namespace PayableServices.Api.Controllers;

[ApiController]
[Authorize(Policy = "VerifiedClient")]
[Route("api/inquiries")]
public sealed class InquiryController(CreateInquiryUseCase create, GetInquiryUseCase get,
    ICurrentActorProvider actor) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateInquiryRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId)) return Unauthorized();
        if (Request.Headers["Idempotency-Key"].Count != 1)
            return BadRequest(ApiResponse.CreateFailure("INVALID_INQUIRY_REQUEST"));
        var result = await create.ExecuteAsync(new(actor.ActorId, request.ServiceId, key ?? "", request.References), cancellationToken);
        if (result.ErrorCode is not null)
            return result.ErrorCode == "IDEMPOTENCY_CONFLICT" ? Conflict(ApiResponse.CreateFailure(result.ErrorCode))
                : BadRequest(ApiResponse.CreateFailure(result.ErrorCode));
        return result.Inquiry!.Outcome == "Pending" ? Accepted(ApiResponse<object>.CreateSuccess(result.Inquiry))
            : Ok(ApiResponse<object>.CreateSuccess(result.Inquiry));
    }

    [HttpGet("{inquiryId:guid}")]
    public async Task<IActionResult> GetAsync(Guid inquiryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId)) return Unauthorized();
        var result = await get.ExecuteAsync(actor.ActorId, inquiryId, cancellationToken);
        return result is null ? NotFound(ApiResponse.CreateFailure("Inquiry not found."))
            : Ok(ApiResponse<object>.CreateSuccess(result));
    }
}
