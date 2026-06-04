using System.Security.Claims;
using Identity.Client.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace Identity.Client.Infrastructure.Security;

public sealed class CurrentActorProvider(IHttpContextAccessor httpContextAccessor) : ICurrentActorProvider
{
    public string GetActorId()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var subject = user?.FindFirst(OpenIddictConstants.Claims.Subject)?.Value
            ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrWhiteSpace(subject) ? "system" : $"user:{subject}";
    }

    public string? GetIpAddress()
    {
        return httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    public string? GetUserAgent()
    {
        return httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
    }
}
