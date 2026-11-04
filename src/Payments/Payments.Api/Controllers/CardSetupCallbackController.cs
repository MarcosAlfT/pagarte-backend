using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments.Api.GrpcClients;
using Utilities.Responses;

namespace Payments.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payment-method-callbacks")]
public sealed class CardSetupCallbackController(CreditCardGrpcClient client, IConfiguration configuration) : ControllerBase
{
	[HttpPost("{integrationId}")]
	[RequestSizeLimit(8192)]
	public async Task<IActionResult> Receive(string integrationId, CancellationToken ct)
	{
		if (integrationId.Length > 100) return Unauthorized(ApiResponse.CreateFailure("INVALID_PROVIDER_CALLBACK"));
		var allowed = configuration.GetSection("PaymentMethods:CallbackHeaders").Get<string[]>()
			?? ["X-Operator-Timestamp", "X-Operator-Signature", "X-Date", "Authorization"];
		var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var name in allowed.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			var value = Request.Headers[name];
			if (value.Count > 1) return Unauthorized(ApiResponse.CreateFailure("INVALID_PROVIDER_CALLBACK"));
			if (value.Count == 1) headers.Add(name, value.ToString());
		}
		if (headers.Count > 10 || headers.Sum(h => h.Key.Length + h.Value.Length) > 4096)
			return Unauthorized(ApiResponse.CreateFailure("INVALID_PROVIDER_CALLBACK"));
		using var body = new MemoryStream();
		var buffer = new byte[1024];
		int read;
		while ((read = await Request.Body.ReadAsync(buffer, ct)) > 0)
		{
			if (body.Length + read > 8192) return StatusCode(413, ApiResponse.CreateFailure("CALLBACK_TOO_LARGE"));
			body.Write(buffer, 0, read);
		}
		try
		{
			await client.ReceiveNotificationAsync(integrationId, headers, body.ToArray(), ct);
			return Ok(ApiResponse.CreateSuccess());
		}
		catch (RpcException error) when (error.StatusCode == Grpc.Core.StatusCode.Unauthenticated && error.Status.Detail == "INVALID_PROVIDER_CALLBACK")
		{
			return Unauthorized(ApiResponse.CreateFailure("INVALID_PROVIDER_CALLBACK"));
		}
	}
}
