using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Pagarte.Security;

public enum ClientResolutionStatus { Found, ProfileRequired, Unavailable }
public sealed record ClientResolution(ClientResolutionStatus Status, Guid? ClientId = null);

public sealed class ClientIdentityResolver(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<ClientResolution> ResolveAsync(
        string issuer, string subject, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/client-identity/resolve")
        {
            Content = JsonContent.Create(new { Issuer = issuer, Subject = subject })
        };
        request.Headers.Add("X-Pagarte-Resolver-Key", configuration["ClientIdentityResolver:ApiKey"]);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(ClientResolutionStatus.ProfileRequired);
            if (!response.IsSuccessStatusCode)
                return new(ClientResolutionStatus.Unavailable);
            var body = await response.Content.ReadFromJsonAsync<ResolveClientResponse>(cancellationToken);
            return body is { ClientId: var id } && id != Guid.Empty
                ? new(ClientResolutionStatus.Found, id)
                : new(ClientResolutionStatus.Unavailable);
        }
        catch (HttpRequestException) { return new(ClientResolutionStatus.Unavailable); }
        catch (JsonException) { return new(ClientResolutionStatus.Unavailable); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(ClientResolutionStatus.Unavailable); }
    }

    private sealed record ResolveClientResponse(Guid ClientId);
}

public static class ClientIdentityResolution
{
    public static IServiceCollection AddClientIdentityResolution(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var address = configuration["ClientIdentityResolver:BaseUrl"]
            ?? throw new InvalidOperationException("ClientIdentityResolver:BaseUrl is required.");
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            throw new InvalidOperationException("Client identity resolver must use HTTPS outside development.");
        if (string.IsNullOrWhiteSpace(configuration["ClientIdentityResolver:ApiKey"]))
            throw new InvalidOperationException("ClientIdentityResolver:ApiKey is required.");
        services.AddHttpClient<ClientIdentityResolver>(client =>
        {
            client.BaseAddress = new Uri(uri.ToString().TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        return services;
    }

    public static IApplicationBuilder UseClientIdentityResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<ClientIdentityResolutionMiddleware>();
}

public sealed class ClientIdentityResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ClientIdentityResolver resolver)
    {
        var requiresClient = context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(attribute => attribute.Policy == KeycloakSecurity.VerifiedClient) == true;
        if (!requiresClient || context.User.Identity?.IsAuthenticated != true)
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

        var resolution = await resolver.ResolveAsync(issuer, subject, context.RequestAborted);
        if (resolution.Status != ClientResolutionStatus.Found)
        {
            context.Response.StatusCode = resolution.Status == ClientResolutionStatus.ProfileRequired
                ? StatusCodes.Status403Forbidden : StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                code = resolution.Status == ClientResolutionStatus.ProfileRequired
                    ? "PROFILE_REQUIRED" : "IDENTITY_RESOLVER_UNAVAILABLE"
            });
            return;
        }

        ((ClaimsIdentity)context.User.Identity!).AddClaim(
            new Claim(KeycloakSecurity.ClientIdClaim, resolution.ClientId!.Value.ToString("D")));
        await next(context);
    }
}
