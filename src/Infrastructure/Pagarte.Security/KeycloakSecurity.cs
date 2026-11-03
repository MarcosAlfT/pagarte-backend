using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Pagarte.Security;

public static class KeycloakSecurity
{
    public const string VerifiedClient = nameof(VerifiedClient);
    public const string VerifiedAdmin = nameof(VerifiedAdmin);
    public const string ClientIdClaim = "pagarte_client_id";

    public static IServiceCollection AddPagarteJwtSecurity(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var authority = configuration["AuthSettings:Authority"]
            ?? throw new InvalidOperationException("AuthSettings:Authority is required.");
        var audience = configuration["AuthSettings:Audience"]
            ?? throw new InvalidOperationException("AuthSettings:Audience is required.");
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) ||
            (issuer.Scheme != Uri.UriSchemeHttps &&
             !(environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.IsLoopback)))
            throw new InvalidOperationException("AuthSettings:Authority must be HTTPS outside development.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority.TrimEnd('/');
                options.Audience = audience;
                options.RequireHttpsMetadata = issuer.Scheme == Uri.UriSchemeHttps;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority.TrimEnd('/'),
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var identity = context.Principal?.Identity as ClaimsIdentity;
                        if (identity is null ||
                            context.Principal?.FindFirst("typ")?.Value != "Bearer" ||
                            string.IsNullOrWhiteSpace(context.Principal.FindFirst("sub")?.Value))
                        {
                            context.Fail("A Keycloak access token is required.");
                            return Task.CompletedTask;
                        }

                        var realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                        if (realmAccess is not null)
                        {
                            try
                            {
                                using var document = JsonDocument.Parse(realmAccess);
                                if (document.RootElement.TryGetProperty("roles", out var roles) &&
                                    roles.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var role in roles.EnumerateArray())
                                    {
                                        if (role.ValueKind == JsonValueKind.String)
                                            identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString()!));
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                                context.Fail("Invalid realm roles.");
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy(VerifiedClient, policy => policy.RequireAuthenticatedUser()
                .RequireClaim("email_verified", "true")
                .RequireAssertion(context => context.User.IsInRole("client") &&
                    !context.User.IsInRole("admin") && !context.User.IsInRole("support")))
            .AddPolicy(VerifiedAdmin, policy => policy.RequireAuthenticatedUser()
                .RequireClaim("email_verified", "true").RequireRole("admin"));
        return services;
    }
}
