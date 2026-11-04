using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Utilities.Responses;

namespace Payments.Api.Controllers;

public sealed class CardRpcExceptionFilter : IExceptionFilter
{
	public void OnException(ExceptionContext context)
	{
		if (context.Exception is not RpcException exception) return;
		var status = exception.StatusCode switch
		{
			StatusCode.InvalidArgument => 400, StatusCode.NotFound => 404, StatusCode.AlreadyExists or StatusCode.Aborted => 409,
			StatusCode.Unauthenticated or StatusCode.PermissionDenied or StatusCode.Unavailable or StatusCode.DeadlineExceeded => 503, _ => 500
		};
		// Do not echo arbitrary downstream errors or provider bodies.
		var safeCode = status switch { 400 => "INVALID_CARD_REQUEST", 404 => "CARD_RESOURCE_NOT_FOUND", 409 => "CARD_CONFLICT",
			503 => "CARD_SERVICE_UNAVAILABLE", _ => "CARD_SERVICE_ERROR" };
		context.Result = new ObjectResult(ApiResponse.CreateFailure(safeCode)) { StatusCode = status };
		context.ExceptionHandled = true;
	}
}
