using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments.Api.DTOs;
using Payments.Api.GrpcClients;
using Utilities.Responses;

namespace Payments.Api.Controllers;

[ApiController]
[Authorize(Policy = "VerifiedClient")]
[Route("api/creditcard/registrations")]
public sealed class CardRegistrationController(CreditCardGrpcClient client) : BaseController
{
	[HttpGet("policy")]
	public async Task<IActionResult> Policy(CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		return Ok(ApiResponse<object>.CreateSuccess(await client.GetRegistrationPolicyAsync(ct)));
	}

	[HttpPost]
	public async Task<IActionResult> Start([FromBody] StartCardRegistrationRequest request, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		return Result(await client.StartRegistrationAsync(GetClientId()!, Key(), request.CardHolderName, request.IsDefault, request.ConsentAccepted, request.ConsentVersion, ct));
	}
	[HttpPost("{id}/complete")]
	public async Task<IActionResult> Complete(string id, [FromBody] CompleteCardRegistrationRequest request, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		return Result(await client.CompleteRegistrationAsync(GetClientId()!, id, Key(), request.TemporaryToken, ct));
	}
	[HttpGet("{id}")]
	public async Task<IActionResult> Get(string id, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		return Result(await client.GetRegistrationAsync(GetClientId()!, id, ct));
	}
	[HttpPost("{id}/continue")]
	public async Task<IActionResult> Continue(string id, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		return Result(await client.ContinueRegistrationAsync(GetClientId()!, id, ct));
	}
	[HttpPost("{id}/cancel")]
	public async Task<IActionResult> Cancel(string id, CancellationToken ct)
	{
		var validation = ValidateClientId();
		if (validation is not null) return validation;
		return Result(await client.CancelRegistrationAsync(GetClientId()!, id, ct));
	}
	private string Key() => Request.Headers["Idempotency-Key"].Count == 1 ? Request.Headers["Idempotency-Key"].ToString() : "";
	private ObjectResult Result(PaymentSwitch.Contracts.CardRegistrationDto result) =>
		StatusCode(result.State switch { "Pending" or "Verifying" or "Unresolved" => 202, "Failed" => 422, "Expired" => 410, _ => 200 },
			new ApiResponse<object> { Data = result, Success = result.State is not ("Failed" or "Expired"), Message = string.IsNullOrEmpty(result.ErrorCode) ? "Operation succeeded." : result.ErrorCode });
}
