using System.Security.Claims;
using Identity.Client.Application.Abstractions;
using Identity.Client.Domain.Users;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Identity.Client.Infrastructure.Security;

public sealed class OpenIddictTokenService(IConfiguration configuration) : ITokenService
{
    public ClaimsPrincipal CreatePrincipal(User user)
    {
        var audiences = GetAudiences();
        var claims = new List<Claim>
        {
            new(OpenIddictConstants.Claims.Subject, user.Id.ToString()),
            new(OpenIddictConstants.Claims.Email, user.Email),
            new(OpenIddictConstants.Claims.Name, user.Email),
            new("security_stamp", user.SecurityStamp)
        };

        claims.AddRange(audiences.Select(audience => new Claim(OpenIddictConstants.Claims.Audience, audience)));

        var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        principal.SetAudiences(audiences);
        principal.SetScopes(OpenIddictConstants.Scopes.Profile, "api");
        principal.SetResources(audiences);
        principal.SetDestinations(static claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Audience => [OpenIddictConstants.Destinations.AccessToken],
            OpenIddictConstants.Claims.Subject => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Email => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Name => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Scope => [OpenIddictConstants.Destinations.AccessToken],
            "security_stamp" => [OpenIddictConstants.Destinations.AccessToken],
            _ => []
        });

        return principal;
    }

    private string[] GetAudiences()
    {
        var audiences = configuration.GetSection("AuthSettings:Audiences").Get<string[]>() ?? [];
        if (audiences.Length == 0)
        {
            var audience = configuration.GetValue<string>("AuthSettings:Audience");
            if (!string.IsNullOrWhiteSpace(audience))
            {
                audiences = [audience];
            }
        }

        return audiences
            .Where(audience => !string.IsNullOrWhiteSpace(audience))
            .Select(audience => audience.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
