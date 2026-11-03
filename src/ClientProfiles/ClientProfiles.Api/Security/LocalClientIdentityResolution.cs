using System.Security.Claims;
using ClientProfiles.Application.UseCases.Clients;
using Microsoft.AspNetCore.Authorization;
using Pagarte.Security;

namespace ClientProfiles.Api.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowProfileCreationAttribute : Attribute { }

public sealed class LocalClientIdentityResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ResolveClientIdUseCase resolveClientId)
    {
        var endpoint = context.GetEndpoint();
        var requiresClient = endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Any(attribute => attribute.Policy == KeycloakSecurity.VerifiedClient) == true;
        if (!requiresClient || endpoint?.Metadata.GetMetadata<AllowProfileCreationAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var issuer = context.User.FindFirst("iss")?.Value;
        var subject = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Guid? clientId;
        try
        {
            clientId = await resolveClientId.ExecuteAsync(issuer, subject, context.RequestAborted);
        }
        catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { code = "IDENTITY_RESOLVER_UNAVAILABLE" });
            return;
        }

        if (clientId is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { code = "PROFILE_REQUIRED" });
            return;
        }

        ((ClaimsIdentity)context.User.Identity!).AddClaim(
            new Claim(KeycloakSecurity.ClientIdClaim, clientId.Value.ToString("D")));
        await next(context);
    }
}
