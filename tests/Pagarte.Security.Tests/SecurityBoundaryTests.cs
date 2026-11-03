using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pagarte.Security;
using Xunit;

namespace Pagarte.Security.Tests;

public sealed class SecurityBoundaryTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuthSettings:Authority"] = "http://localhost:8080/realms/pagarte",
            ["AuthSettings:Audience"] = "payments-api",
            ["ClientIdentityResolver:ApiKey"] = "test-only-key",
            ["ClientIdentityResolver:BaseUrl"] = "http://localhost:18082"
        }).Build();

    private static readonly IHostEnvironment Development = new TestEnvironment(Environments.Development);

    [Theory]
    [InlineData("client", true, true)]
    [InlineData("client", false, false)]
    [InlineData("support", true, false)]
    [InlineData("admin", true, false)]
    public async Task VerifiedClient_requires_verified_client_without_operator_role(
        string role, bool verified, bool expected)
    {
        var services = new ServiceCollection().AddLogging()
            .AddPagarteJwtSecurity(Configuration(), Development).BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var principal = Principal(role, verified);
        var result = await authorization.AuthorizeAsync(principal, KeycloakSecurity.VerifiedClient);
        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task VerifiedAdmin_rejects_client()
    {
        var services = new ServiceCollection().AddLogging()
            .AddPagarteJwtSecurity(Configuration(), Development).BuildServiceProvider();
        var authorization = services.GetRequiredService<IAuthorizationService>();
        Assert.False((await authorization.AuthorizeAsync(
            Principal("client", true), KeycloakSecurity.VerifiedAdmin)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            Principal("admin", true), KeycloakSecurity.VerifiedAdmin)).Succeeded);

        var mixed = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, "client"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("email_verified", "true")
        }, "test"));
        Assert.False((await authorization.AuthorizeAsync(
            mixed, KeycloakSecurity.VerifiedClient)).Succeeded);
    }

    [Fact]
    public void Production_rejects_http_authority_and_resolver()
    {
        var production = new TestEnvironment(Environments.Production);
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddPagarteJwtSecurity(Configuration(), production));
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddClientIdentityResolution(Configuration(), production));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ClientResolutionStatus.ProfileRequired)]
    [InlineData(HttpStatusCode.Unauthorized, ClientResolutionStatus.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ClientResolutionStatus.Unavailable)]
    public async Task Resolver_fails_closed(HttpStatusCode status, ClientResolutionStatus expected)
    {
        using var client = new HttpClient(new StubHandler(status))
        { BaseAddress = new Uri("https://localhost/") };
        var resolver = new ClientIdentityResolver(client, Configuration());
        var result = await resolver.ResolveAsync("issuer", "subject", CancellationToken.None);
        Assert.Equal(expected, result.Status);
        Assert.Null(result.ClientId);
    }

    private static ClaimsPrincipal Principal(string role, bool verified) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, role),
            new Claim("email_verified", verified ? "true" : "false")
        }, "test"));

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Pagarte.Security.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
