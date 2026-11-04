using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pagarte.Security;
using Payments.Api.GrpcClients;
using Utilities.Responses;

namespace Payments.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CardReviewRequest(string Action, string ExpectedState, string ReasonCode);

[ApiController]
[Authorize(Policy = KeycloakSecurity.VerifiedAdmin)]
[Route("api/admin/payment-methods")]
public sealed class CardOperationsController(CardOperationsGrpcClient client) : ControllerBase
{
	[HttpGet("reviews")]
	public async Task<IActionResult> List([FromQuery] int limit = 50, CancellationToken ct = default) =>
		Ok(ApiResponse<object>.CreateSuccess(await client.ListAsync(limit, ct)));
	[HttpGet("reviews/{id}/audit")]
	public async Task<IActionResult> Audit(string id, CancellationToken ct) => Ok(ApiResponse<object>.CreateSuccess(await client.GetAsync(id, ct)));
	[HttpPost("reviews/{kind}/{id}")]
	public async Task<IActionResult> Review(string kind, string id, CardReviewRequest request, CancellationToken ct)
	{
		var issuer = User.FindFirst("iss")?.Value; var subject = User.FindFirst("sub")?.Value;
		if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)) return Forbid();
		// Encode the authenticated tuple unambiguously; the request cannot choose its audit actor.
		var actor = System.Text.Json.JsonSerializer.Serialize(new[] { issuer, subject });
		var result = await client.ReviewAsync(new() { Actor = actor, IdempotencyKey = Request.Headers["Idempotency-Key"].Count == 1 ? Request.Headers["Idempotency-Key"].ToString() : "",
			TargetId = id, Kind = kind, Action = request.Action, ExpectedState = request.ExpectedState, ReasonCode = request.ReasonCode }, ct);
		return StatusCode(result.Outcome == "Pending" ? 202 : 200, ApiResponse<object>.CreateSuccess(result));
	}
}
