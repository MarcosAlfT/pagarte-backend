using ExternalConnections.CompanyInquiries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PayableServices.Tests;

public sealed class DevelopmentGuardTests
{
    [Fact]
    public void Production_cannot_start_the_mock_or_enable_its_adapter()
    {
        Assert.Throws<InvalidOperationException>(() => MockCompany.Api.Program.Build(["--environment", "Production"]));
        var host = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production" });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CompanyProviders:MockCompany:Enabled"] = "true"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => host.Services.AddCompanyInquiries(configuration, host.Environment));
    }
    [Fact]
    public void Recreating_mock_cache_preserves_provider_identifiers_and_timestamps()
    {
        var clock = new TestClock();
        var request = new MockCompany.Api.MockInquiry("ELEC-EC", new() { ["accountNumber"] = "10000001" }, clock.UtcNow);
        var first = new MockCompany.Api.MockResponses(clock).Inquire("stored-provider-key", request);
        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        var replay = new MockCompany.Api.MockResponses(clock).Inquire("stored-provider-key", request);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(((Microsoft.AspNetCore.Http.IValueHttpResult)first).Value),
            System.Text.Json.JsonSerializer.Serialize(((Microsoft.AspNetCore.Http.IValueHttpResult)replay).Value));
    }
}
