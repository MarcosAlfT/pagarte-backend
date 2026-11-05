using System.Text.Json;
using System.Text.Json.Serialization;
using Grpc.Core;
using GrpcStatusCode = Grpc.Core.StatusCode;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pagarte.Security;
using Payments.Api.GrpcClients;
using Utilities.Responses;

namespace Payments.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PaymentRecoveryRequest(string ExpectedVersion, string ReasonCode);

[ApiController]
[Authorize(Policy = KeycloakSecurity.VerifiedAdmin)]
[Route("api/admin/payments/recovery")]
public sealed class PaymentRecoveryController(PaymentRecoveryGrpcClient client) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] int limit = 50, CancellationToken cancellationToken = default) =>
        RunAsync(async () => Ok(ApiResponse<object>.CreateSuccess(await client.ListAsync(limit, cancellationToken))));

    [HttpGet("audit/{id:guid}")]
    public Task<IActionResult> Audit(Guid id, CancellationToken cancellationToken) =>
        RunAsync(async () => Ok(ApiResponse<object>.CreateSuccess(await client.GetAuditAsync(id.ToString(), cancellationToken))));

    [HttpPost("{paymentId:guid}")]
    public Task<IActionResult> Schedule(Guid paymentId, PaymentRecoveryRequest request, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var issuer = User.FindFirst("iss")?.Value;
            var subject = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)) return Forbid();
            var result = await client.ScheduleAsync(new()
            {
                PaymentId = paymentId.ToString(), Actor = JsonSerializer.Serialize(new[] { issuer, subject }),
                IdempotencyKey = Request.Headers["Idempotency-Key"].Count == 1 ? Request.Headers["Idempotency-Key"].ToString() : "",
                ExpectedVersion = request.ExpectedVersion, ReasonCode = request.ReasonCode
            }, cancellationToken);
            return Accepted(ApiResponse<object>.CreateSuccess(result));
        });

    private async Task<IActionResult> RunAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (RpcException error)
        {
            var (status, code) = error.StatusCode switch
            {
                GrpcStatusCode.InvalidArgument => (400, "INVALID_RECOVERY_REQUEST"),
                GrpcStatusCode.NotFound => (404, "RECOVERY_RESOURCE_NOT_FOUND"),
                GrpcStatusCode.Aborted or GrpcStatusCode.AlreadyExists => (409, "PAYMENT_RECOVERY_CONFLICT"),
                GrpcStatusCode.Unauthenticated or GrpcStatusCode.PermissionDenied or GrpcStatusCode.Unavailable or GrpcStatusCode.DeadlineExceeded
                    => (503, "PAYMENT_RECOVERY_UNAVAILABLE"),
                _ => (500, "PAYMENT_RECOVERY_ERROR")
            };
            return StatusCode(status, ApiResponse.CreateFailure(code));
        }
    }
}
