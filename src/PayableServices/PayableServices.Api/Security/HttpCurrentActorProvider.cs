using PayableServices.Application.Abstractions;

namespace PayableServices.Api.Security;

public sealed class HttpCurrentActorProvider(IHttpContextAccessor httpContextAccessor)
	: ICurrentActorProvider
{
	public string? ActorId =>
		httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

	public string? IpAddress =>
		httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
