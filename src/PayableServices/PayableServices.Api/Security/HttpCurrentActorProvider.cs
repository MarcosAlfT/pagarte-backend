using PayableServices.Application.Abstractions;
using Pagarte.Security;

namespace PayableServices.Api.Security;

public sealed class HttpCurrentActorProvider(IHttpContextAccessor httpContextAccessor)
	: ICurrentActorProvider
{
	public string? ActorId =>
		httpContextAccessor.HttpContext?.User.FindFirst(KeycloakSecurity.ClientIdClaim)?.Value;

	public string? IpAddress =>
		httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
