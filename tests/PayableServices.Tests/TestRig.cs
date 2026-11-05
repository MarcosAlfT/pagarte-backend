using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PayableServices.Application.Abstractions;
using PayableServices.Persistence;

namespace PayableServices.Tests;

public sealed class TestClock : TimeProvider, IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    public override DateTimeOffset GetUtcNow() => new(UtcNow);
}

public sealed class ProviderState
{
    public int Calls;
    public string? Body;
    public bool Timeout;
    public readonly List<string> Keys = [];
    public TaskCompletionSource? Entered;
    public TaskCompletionSource? Release;
}

public sealed class Faults : SaveChangesInterceptor
{
    public bool FailCompletion;
    public bool FailQuote;
    public bool FailExecutionCommit;
    public bool FailSubmissionCommit;
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context!;
        if (FailSubmissionCommit && db.ChangeTracker.Entries<PayableServices.Domain.Entities.Quote>().Any(x =>
            x.State == EntityState.Modified && x.Entity.SubmissionId is not null))
        { FailSubmissionCommit = false; throw new DbUpdateException("Synthetic submission commit failure."); }
        if (FailCompletion && db.ChangeTracker.Entries<PayableServices.Domain.Entities.AmountInquiry>().Any(x =>
            x.State == EntityState.Modified && x.Entity.Outcome != PayableServices.Domain.Entities.InquiryOutcome.Pending))
        { FailCompletion = false; throw new DbUpdateException("Synthetic completion persistence failure."); }
        if (FailQuote && db.ChangeTracker.Entries<PayableServices.Domain.Entities.Quote>().Any(x => x.State == EntityState.Added))
        { FailQuote = false; throw new DbUpdateException("Synthetic quote persistence failure."); }
        if (FailExecutionCommit && db.ChangeTracker.Entries<PayableServices.Domain.Entities.Quote>().Any(x =>
            x.State == EntityState.Modified && x.Entity.PaymentId is not null))
        { FailExecutionCommit = false; throw new DbUpdateException("Synthetic execution persistence failure."); }
        return ValueTask.FromResult(result);
    }
}

public sealed class TestRig : IAsyncDisposable
{
    public const string Owner = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    public static readonly Guid CountryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly string databaseName = "PagarteInquiryTests_" + Guid.NewGuid().ToString("N");
    private string Connection => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("PAGARTE_TEST_SQL") ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true")
    { InitialCatalog = databaseName }.ConnectionString;
    public readonly TestClock Clock = new();
    public readonly ProviderState Provider = new();
    public readonly Faults Faults = new();
    public readonly ExecutionProbe Execution = new();
    public IConfigurationRoot Settings { get; private set; } = null!;
    private Microsoft.AspNetCore.Builder.WebApplication mock = null!;
    private Microsoft.AspNetCore.Builder.WebApplication api = null!;
    public IServiceProvider Services => api.Services;
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes("synthetic-test-signing-key-with-at-least-32-bytes"));
    public HttpClient Admin { get; private set; } = null!;
    public Guid ServiceId { get; private set; }
    public Guid RouteId { get; private set; }

    public void SetBill(string account, string period, long amount, string currency = "USD") =>
        mock.Services.GetRequiredService<MockCompany.Api.MockResponses>().SetBill("ELEC-EC", account, new(period, amount, currency));

    public static async Task<TestRig> CreateAsync(bool activate = true)
    {
        var rig = new TestRig();
        rig.Execution.Now = () => rig.Clock.UtcNow;
        rig.mock = MockCompany.Api.Program.Build(["--environment", "Development", "--MockCompany:ApiKey", "synthetic-test-key"], builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<TimeProvider>(rig.Clock);
        });
        await rig.mock.StartAsync();
        rig.InitializeSettings();
        var arguments = rig.Settings.AsEnumerable().Where(x => x.Value is not null).SelectMany(x => new[] { "--" + x.Key, x.Value! })
            .Concat(new[] { "--environment", "Development" }).ToArray();
        rig.api = await PayableServices.Api.Program.BuildAsync(arguments, rig.ConfigureApi);
        await rig.api.StartAsync();
        try
        {
            rig.Admin = rig.Client("admin");
            if (activate)
            {
                var sync = await rig.Admin.PostAsJsonAsync("/api/catalogue/maintenance/sync", new { sourceName = "MockCompany", countryId = CountryId });
                Assert.Equal(HttpStatusCode.OK, sync.StatusCode);
                await rig.InDb(async db =>
                {
                    var route = await db.PaymentRoutes.SingleAsync(x => x.ExternalSourceCode == "MockCompany" && x.ExternalServiceCode == "ELEC-EC");
                    rig.RouteId = route.Id;
                    rig.ServiceId = route.PayableServiceId;
                });
                var activation = await rig.Admin.PostAsJsonAsync("/api/payment-routes/activate", new { paymentRouteId = rig.RouteId });
                Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
            }
            return rig;
        }
        catch { await rig.DisposeAsync(); throw; }
    }

    private void InitializeSettings()
    {
        Settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PayableServicesDb"] = Connection,
            ["AuthSettings:Authority"] = "https://issuer.test/realms/pagarte",
            ["AuthSettings:Audience"] = "payable-services-api",
            ["ClientIdentityResolver:BaseUrl"] = "https://resolver.test",
            ["ClientIdentityResolver:ApiKey"] = "synthetic-test-key",
            ["CompanyProviders:MockCompany:Enabled"] = "true",
            ["CompanyProviders:MockCompany:BaseUrl"] = "http://localhost/",
            ["CompanyProviders:MockCompany:ApiKey"] = "synthetic-test-key"
        }).Build();
    }

    private void ConfigureApi(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder)
    {
        builder.WebHost.UseTestServer();
        builder.Configuration.AddConfiguration(Settings);
        var services = builder.Services;
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IPaymentExecutionClient>(Execution);
        services.AddDbContext<PayableServicesDbContext>(options => options.UseSqlServer(Connection).AddInterceptors(Faults));
        services.AddHttpClient("CompanyInquiry").ConfigurePrimaryHttpMessageHandler(() => new ProviderHandler(mock.GetTestServer().CreateHandler(), Provider));
        services.AddHttpClient<Pagarte.Security.ClientIdentityResolver>().ConfigurePrimaryHttpMessageHandler(() => new ResolverHandler());
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Configuration = new OpenIdConnectConfiguration { Issuer = "https://issuer.test/realms/pagarte" };
            options.Configuration.SigningKeys.Add(Key);
        });
    }

    public HttpClient Client(string? role = "client", string owner = Owner, bool verified = true)
    {
        var client = api.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        if (role is not null)
        {
            var token = new JwtSecurityToken("https://issuer.test/realms/pagarte", "payable-services-api", [
                new Claim("sub", owner), new Claim("typ", "Bearer"),
                new Claim("email_verified", verified ? "true" : "false"),
                new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } }))
            ], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30), new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }

    public Task<HttpResponseMessage> Inquire(HttpClient client, string account = "10000001", string? key = null, Guid? serviceId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inquiries")
        {
            Content = JsonContent.Create(new { serviceId = serviceId ?? ServiceId, references = new[] { new { key = "accountNumber", value = account } } })
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return client.SendAsync(request);
    }
    public Task<HttpResponseMessage> Quote(HttpClient client, Guid inquiryId) => client.PostAsJsonAsync("/api/quotes", new { inquiryId });
    public static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }
    public async Task InDb(Func<PayableServicesDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<PayableServicesDbContext>());
    }
    public async Task Recover(Guid id)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PayableServices.Application.UseCases.CreateInquiryUseCase>().RecoverAsync(id);
    }

    public async Task RecoverQuote(Guid id)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PayableServices.Application.UseCases.ConfirmQuoteUseCase>().RecoverAsync(id);
    }

    public async Task RestartApiAsync()
    {
        await api.StopAsync();
        await api.DisposeAsync();
        var args = Settings.AsEnumerable().Where(x => x.Value is not null)
            .SelectMany(x => new[] { "--" + x.Key, x.Value! })
            .Concat(new[] { "--environment", "Development" }).ToArray();
        api = await PayableServices.Api.Program.BuildAsync(args, ConfigureApi);
        await api.StartAsync();
    }
    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
        await mock.DisposeAsync();
        // Only the unique database created by this test rig is removed.
        await using var db = new PayableServicesDbContext(new DbContextOptionsBuilder<PayableServicesDbContext>().UseSqlServer(Connection).Options);
        await db.Database.EnsureDeletedAsync();
    }

    private sealed class ResolverHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { clientId = body.GetProperty("subject").GetString() }) };
        }
    }
    private sealed class ProviderHandler(HttpMessageHandler inner, ProviderState state) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/inquiries")
            {
                Interlocked.Increment(ref state.Calls);
                lock (state.Keys) state.Keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
                state.Entered?.TrySetResult();
                if (state.Release is not null) await state.Release.Task.WaitAsync(cancellationToken);
                if (state.Timeout) throw new TaskCanceledException("Synthetic transport timeout.");
                if (state.Body is not null) return new(HttpStatusCode.OK) { Content = new StringContent(state.Body, Encoding.UTF8, "application/json") };
            }
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
