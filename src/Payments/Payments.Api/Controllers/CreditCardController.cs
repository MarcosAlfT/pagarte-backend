using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments.Api.DTOs;
using Payments.Api.GrpcClients;
using Utilities.Responses;

namespace Payments.Api.Controllers;

[ApiController]
[Authorize(Policy = "VerifiedClient")]
[Route("api/creditcard")]
public sealed class CreditCardController(CreditCardGrpcClient grpcClient) : BaseController
{
	[HttpGet]
	public async Task<IActionResult> GetAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		var result = await grpcClient.GetCardsAsync(GetClientId()!, page, pageSize, ct);
		return Ok(ApiResponse<object>.CreateSuccess(result.Cards));
	}

	[HttpGet("{cardId}")]
	public async Task<IActionResult> GetByIdAsync(string cardId, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		var result = await grpcClient.GetCardAsync(cardId, GetClientId()!, ct);
		return result.Found ? Ok(ApiResponse<object>.CreateSuccess(result.Card))
			: NotFound(ApiResponse.CreateFailure("CARD_NOT_FOUND"));
	}

	// The former raw-card endpoint is retired. Do not bind or forward its body.
	[HttpPost]
	public IActionResult Register() => BadRequest(ApiResponse.CreateFailure("USE_CARD_REGISTRATION_SESSION"));

	[HttpPut("{cardId}")]
	public async Task<IActionResult> UpdateAsync(string cardId, [FromBody] UpdateCreditCardRequest request, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		await grpcClient.UpdateCardAsync(cardId, GetClientId()!, request.CardHolderName, request.IsDefault, ct);
		return Ok(ApiResponse.CreateSuccess("Card updated successfully."));
	}

	[HttpGet("{cardId}/removal")]
	public async Task<IActionResult> RemovalAsync(string cardId, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		return Ok(ApiResponse<object>.CreateSuccess(await grpcClient.GetRemovalAsync(GetClientId()!, cardId, ct)));
	}

	[HttpDelete("{cardId}")]
	public async Task<IActionResult> DeleteAsync(string cardId, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		await grpcClient.DeleteCardAsync(cardId, GetClientId()!, ct);
		return Ok(ApiResponse.CreateSuccess("Card removed locally. Provider cleanup is processed asynchronously."));
	}
}
