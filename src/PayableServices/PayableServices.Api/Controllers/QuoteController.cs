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
[Route("api/quotes")]
public sealed class QuoteController(
    CreateQuoteUseCase createQuoteUseCase,
    ConfirmQuoteUseCase confirmQuoteUseCase,
    GetQuoteUseCase getQuoteUseCase,
    ListQuotesUseCase listQuotesUseCase,
    ICurrentActorProvider currentActorProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] QuoteHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var clientId = GetRequiredClientId();
        if (clientId is null)
        {
            return Unauthorized(ApiResponse.CreateFailure("Authenticated client identifier is missing."));
        }

        if (!query.IsValid || Request.Query.Any(pair => pair.Value.Count != 1 || string.IsNullOrWhiteSpace(pair.Value[0])))
        {
            return BadRequest(ApiResponse.CreateFailure("Invalid quote filters or pagination."));
        }

        var result = await listQuotesUseCase.ExecuteAsync(clientId, query, cancellationToken);
        return Ok(ApiResponse<object>.CreateSuccess(result));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var clientId = GetRequiredClientId();
        if (clientId is null)
        {
            return Unauthorized(ApiResponse.CreateFailure("Authenticated client identifier is missing."));
        }

        var result = await createQuoteUseCase.ExecuteAsync(
            new CreateQuoteCommand(
                clientId,
                request.ServiceId,
                request.Currency,
                request.ReferenceValues
                    ?? new Dictionary<string, string>(StringComparer.Ordinal), request.InquiryId),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(ApiResponse.CreateFailure(result.ErrorMessage));
        }

        return Ok(ApiResponse<object>.CreateSuccess(
            result.Quote,
            "Quote created successfully."));
    }

    [HttpGet("{quoteId:guid}")]
    public async Task<IActionResult> GetAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var clientId = GetRequiredClientId();
        if (clientId is null)
        {
            return Unauthorized(ApiResponse.CreateFailure("Authenticated client identifier is missing."));
        }

        var quote = await getQuoteUseCase.ExecuteAsync(clientId, quoteId, cancellationToken);
        return quote is null
            ? NotFound(ApiResponse.CreateFailure("Quote not found."))
            : Ok(ApiResponse<object>.CreateSuccess(quote));
    }

    [HttpPost("{quoteId:guid}/confirm")]
    public async Task<IActionResult> ConfirmAsync(
        Guid quoteId,
        [FromBody] ConfirmQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var clientId = GetRequiredClientId();
        if (clientId is null)
        {
            return Unauthorized(ApiResponse.CreateFailure("Authenticated client identifier is missing."));
        }

        var result = await confirmQuoteUseCase.ExecuteAsync(
            new ConfirmQuoteCommand(clientId, quoteId, request.CreditCardId),
            cancellationToken);
        return MapExecutionResult(result);
    }

    [HttpGet("{quoteId:guid}/execution")]
    public async Task<IActionResult> GetExecutionAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var clientId = GetRequiredClientId();
        if (clientId is null)
        {
            return Unauthorized(ApiResponse.CreateFailure("Authenticated client identifier is missing."));
        }

        var result = await confirmQuoteUseCase.RefreshStatusAsync(
            clientId,
            quoteId,
            cancellationToken);
        return MapExecutionResult(result);
    }

    private IActionResult MapExecutionResult(ConfirmQuoteResult result)
    {
        var response = new
        {
            result.PaymentId,
            result.Reference,
            Status = result.Status.ToString(),
            result.ErrorCode,
            result.ErrorMessage,
            result.Replayed,
            result.Final
        };

        if (result.Accepted)
        {
            return result.Final
                ? Ok(ApiResponse<object>.CreateSuccess(response))
                : Accepted(ApiResponse<object>.CreateSuccess(response, "Payment execution is processing."));
        }

        var failure = new ApiResponse<object>
        {
            Success = false,
            Message = result.ErrorMessage,
            Data = response
        };
        return result.ErrorCode == "QUOTE_NOT_FOUND"
            ? NotFound(failure)
            : BadRequest(failure);
    }

    private string? GetRequiredClientId() =>
        string.IsNullOrWhiteSpace(currentActorProvider.ActorId)
            ? null
            : currentActorProvider.ActorId;
}
