using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PaymentSwitch.Contracts;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Infrastructure;

namespace PaymentSwitch.Processor.Tests;

public sealed class CardTestClock : IClock
{
	public DateTime UtcNow { get; set; } = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
	public void Advance(TimeSpan delta) => UtcNow += delta;
}

public sealed class CardTestFaults : SaveChangesInterceptor
{
	public bool FailCardCommit;
	public bool FailRemovalCommit;
	public bool FailReviewCommit;
	public bool FailDefaultCommit;
	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
		InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		if (FailReviewCommit && eventData.Context!.ChangeTracker.Entries<CardReviewOperation>().Any(e => e.State == EntityState.Modified && e.Entity.CompletedAt != null))
		{
			FailReviewCommit = false;
			throw new DbUpdateException("Synthetic review-result commit failure.");
		}
		if (FailRemovalCommit && eventData.Context!.ChangeTracker.Entries<CreditCard>().Any(e => e.State == EntityState.Modified && e.Entity.DetachmentState == "Removed"))
		{
			FailRemovalCommit = false;
			throw new DbUpdateException("Synthetic removal-commit failure.");
		}
		if (FailDefaultCommit && eventData.Context!.ChangeTracker.Entries<CreditCard>().Any(e => e.State == EntityState.Modified && e.Entity.IsDefault))
		{
			FailDefaultCommit = false;
			throw new DbUpdateException("Synthetic default-commit failure.");
		}
		if (FailCardCommit && eventData.Context!.ChangeTracker.Entries<CreditCard>().Any(e => e.State == EntityState.Added))
		{
			FailCardCommit = false;
			throw new DbUpdateException("Synthetic card-commit failure.");
		}
		return ValueTask.FromResult(result);
	}
}

public sealed class CardTestRig : IAsyncDisposable
{
	public const string Owner = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
	public const string Foreign = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
	public const string CallbackKey = "synthetic-callback-signing-key-at-least-32-bytes";
	public const string OperationsKey = "synthetic-separate-operations-key";
	public const string WorkloadKey = "synthetic-card-test-workload-key";
	private const string Issuer = "https://card-issuer.test/realms/pagarte";
	private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("synthetic-card-test-signing-key-32-bytes"));
	private readonly string databaseName = "PagarteCardTests_" + Guid.NewGuid().ToString("N");
	private readonly string journalDirectory = Path.Combine(Path.GetTempPath(), "pagarte-card-tests-" + Guid.NewGuid().ToString("N"));
	private readonly IDataProtectionProvider protection = new EphemeralDataProtectionProvider();
	private WebApplication processor = null!;
	private WebApplication api = null!;
	public readonly CardTestClock Clock = new();
	public readonly CardTestFaults Faults = new();
	public readonly CardTestCompany Company = new();
	public IConfigurationRoot Settings { get; private set; } = null!;
	public IServiceProvider Services => processor.Services;
	public bool MissingProfile;
	private string Connection => new SqlConnectionStringBuilder(
		Environment.GetEnvironmentVariable("PAGARTE_TEST_SQL") ?? @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true")
		{ InitialCatalog = databaseName }.ConnectionString;

	public static async Task<CardTestRig> CreateAsync(bool legacyCard = false, bool duplicateLegacyDefaults = false)
	{
		var rig = new CardTestRig();
		var values = new Dictionary<string, string?>
		{
			["CardOperationsAuth:ApiKey"] = OperationsKey, ["ConnectionStrings:PaymentDb"] = rig.Connection, ["PaymentOperator:Provider"] = "Mock",
			["PaymentOperatorCallbackSecrets:synthetic-callback"] = CallbackKey,
			["PaymentMethods:TokenDigestKey"] = "synthetic-card-digest-key-with-at-least-32-bytes",
			["PaymentOperators:MockStateDirectory"] = rig.journalDirectory,
			["PaymentOperators:DefaultIntegration"] = "mock-a", ["WorkloadAuth:ApiKey"] = WorkloadKey,
			["PaymentSwitchProcessor:GrpcUrl"] = "http://localhost",
			["AuthSettings:Authority"] = Issuer, ["AuthSettings:Audience"] = "payments-api",
			["ClientIdentityResolver:BaseUrl"] = "https://card-resolver.test",
			["ClientIdentityResolver:ApiKey"] = "synthetic-resolver-key",
			["Logging:LogLevel:Default"] = "Warning"
		};
		foreach (var id in new[] { "mock-a", "mock-b" })
		{
			values[$"PaymentOperators:Integrations:{id}:Adapter"] = "Mock";
			values[$"PaymentOperators:Integrations:{id}:Account"] = "synthetic-" + id;
			values[$"PaymentOperators:Integrations:{id}:Environment"] = "Development";
			values[$"PaymentOperators:Integrations:{id}:Version"] = "1";
			values[$"PaymentOperators:Integrations:{id}:CallbackKeyReference"] = "synthetic-callback";
			values[$"PaymentOperators:Integrations:{id}:RegistrationEnabled"] = "true";
			values[$"PaymentOperators:Integrations:{id}:SupportedCountries:0"] = "EC";
			values[$"PaymentOperators:Integrations:{id}:SupportedCurrencies:0"] = "USD";
		}
		rig.Settings = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
		try
		{
			if (legacyCard || duplicateLegacyDefaults)
			{
				await using var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>().UseSqlServer(rig.Connection).Options);
				await db.GetService<IMigrator>().MigrateAsync("20260910063239_ExpandCatalogueMoneyPrecision");
				var count = duplicateLegacyDefaults ? 2 : 1;
				for (var i = 0; i < count; i++) await db.Database.ExecuteSqlRawAsync("""
					INSERT INTO CreditCards (Id, ClientId, OperatorProvider, OperatorCardToken, CardNumber, CardHolderName,
					 Last4Digits, CardType, ExpiryMonth, ExpiryYear, IsDefault, CreatedAt, IsDeleted)
					VALUES (NEWID(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Mock', 'synthetic-legacy-reference',
					 'synthetic-legacy-number', 'Legacy display', '4242', 0, 12, 2040, 1, GETUTCDATE(), 0)
					""");
			}
			await rig.StartAsync(); return rig;
		}
		catch { await rig.DisposeAsync(); throw; }
	}

	private string[] Args() => Settings.AsEnumerable().Where(x => x.Value is not null)
		.SelectMany(x => new[] { "--" + x.Key, x.Value! }).Concat(["--environment", "Development"]).ToArray();

	private async Task StartAsync()
	{
		processor = await PaymentSwitch.Processor.Program.BuildAsync(Args(), builder =>
		{
			builder.WebHost.UseTestServer();
			builder.Configuration.AddConfiguration(Settings);
			builder.Services.RemoveAll<IHostedService>();
			builder.Services.AddSingleton<IClock>(Clock);
			builder.Services.AddSingleton<ICompanyPaymentGateway>(Company);
			builder.Services.AddSingleton(protection);
			builder.Services.AddDbContext<PaymentDbContext>(options => options.UseSqlServer(Connection).AddInterceptors(Faults));
		});
		await processor.StartAsync();
		api = Payments.Api.Program.Build(Args(), builder =>
		{
			builder.WebHost.UseTestServer();
			builder.Configuration.AddConfiguration(Settings);
			builder.Services.AddGrpcClient<CreditCardService.CreditCardServiceClient>()
				.ConfigurePrimaryHttpMessageHandler(() => processor.GetTestServer().CreateHandler());
			builder.Services.AddGrpcClient<CardOperationsService.CardOperationsServiceClient>()
				.ConfigurePrimaryHttpMessageHandler(() => processor.GetTestServer().CreateHandler());
			builder.Services.AddHttpClient<Pagarte.Security.ClientIdentityResolver>()
				.ConfigurePrimaryHttpMessageHandler(() => new ResolverHandler(this));
			builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
			{
				options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
				options.Configuration.SigningKeys.Add(SigningKey);
			});
		});
		await api.StartAsync();
	}

	public async Task RestartAsync()
	{
		await api.DisposeAsync();
		await processor.DisposeAsync();
		await StartAsync();
	}

	public HttpClient Client(string? owner = Owner, bool verified = true, string role = "client")
	{
		var client = api.GetTestClient();
		client.BaseAddress = new Uri("https://localhost");
		if (owner is not null)
		{
			var token = new JwtSecurityToken(Issuer, "payments-api", [
				new Claim("sub", owner), new Claim("typ", "Bearer"), new Claim("email_verified", verified ? "true" : "false"),
				new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } })) ],
				DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30), new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
		}
		return client;
	}
	public GrpcChannel Channel() => GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = processor.GetTestServer().CreateHandler() });

	public async Task<JsonElement> Start(HttpClient client, string? key = null, bool isDefault = false) =>
		await Data(await Send(client, "/api/creditcard/registrations", new { cardHolderName = "Test Card", isDefault, consentAccepted = true, consentVersion = "saved-card-v1" }, key ?? Guid.NewGuid().ToString("N")));
	public Task<HttpResponseMessage> Complete(HttpClient client, JsonElement session, string scenario = "success", string? key = null, string? token = null) =>
		Send(client, $"/api/creditcard/registrations/{session.GetProperty("id").GetString()}/complete",
			new { temporaryToken = token ?? Token(session, scenario) }, key ?? Guid.NewGuid().ToString("N"));
	public static string Token(JsonElement session, string scenario) =>
		$"mock:{session.GetProperty("provider").GetString()}:{Guid.Parse(session.GetProperty("id").GetString()!):N}:{scenario}";
	public static Task<HttpResponseMessage> Send(HttpClient client, string path, object body, string? key = null)
	{
		var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
		if (key is not null) request.Headers.Add("Idempotency-Key", key);
		return client.SendAsync(request);
	}
	public static async Task<JsonElement> Data(HttpResponseMessage response, HttpStatusCode? expected = null)
	{
		var body = await response.Content.ReadAsStringAsync();
		if (expected is not null) Assert.Equal(expected.Value, response.StatusCode);
		else Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
		return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
	}
	public async Task<PaymentExecutionResult> Accept(Guid cardId, string country = "EC")
	{
		using var scope = Services.CreateScope();
		var execution = new ConfirmedQuoteExecution(Owner, Guid.NewGuid(), cardId, Guid.NewGuid(), "Test Service",
			Guid.NewGuid(), country, Guid.NewGuid(), "TestRoute", null, null, "Test", "TEST-SERVICE",
			"https://company.test/pay", "https://company.test/rollback", "https://company.test/status/{reference}", "test-credential",
			Clock.UtcNow, Clock.UtcNow.AddMinutes(15), "ToEven", 1000, "USD",
			[new ConfirmedQuoteComponent("ServiceAmount", "Service", "Catalogue", true, 1, 1000, "USD")],
			[new ConfirmedQuoteReference("account", "synthetic")]);
		return await scope.ServiceProvider.GetRequiredService<AcceptConfirmedQuoteUseCase>().ExecuteAsync(execution);
	}

	public async Task<HttpResponseMessage> Notify(Guid sessionId, string eventId, string integrationId = "mock-a", bool valid = true, DateTime? timestamp = null)
	{
		using var client = Client(null);
		var payload = JsonSerializer.SerializeToUtf8Bytes(new { sessionId = sessionId.ToString(), eventId });
		var sentAt = new DateTimeOffset(timestamp ?? Clock.UtcNow).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
		var signed = Encoding.UTF8.GetBytes("mock-callback-v1\n" + sentAt + ".").Concat(payload).ToArray();
		var signature = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(CallbackKey), signed));
		using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/payment-method-callbacks/{integrationId}") { Content = new ByteArrayContent(payload) };
		request.Headers.Add("X-Operator-Timestamp", sentAt);
		request.Headers.Add("X-Operator-Signature", valid ? signature : new string('0', 64));
		return await client.SendAsync(request);
	}

	public async Task InDb(Func<PaymentDbContext, Task> action)
	{
		using var scope = Services.CreateScope();
		await action(scope.ServiceProvider.GetRequiredService<PaymentDbContext>());
	}
	public async Task Recover(Guid id)
	{
		using var scope = Services.CreateScope();
		await scope.ServiceProvider.GetRequiredService<ContinueCardRegistrationUseCase>().ExecuteAsync(id);
	}
	public async ValueTask DisposeAsync()
	{
		if (api is not null) await api.DisposeAsync();
		if (processor is not null) await processor.DisposeAsync();
		if (!databaseName.StartsWith("PagarteCardTests_", StringComparison.Ordinal)) throw new InvalidOperationException();
		await using var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>().UseSqlServer(Connection).Options);
		await db.Database.EnsureDeletedAsync();
		var directory = Path.GetFullPath(journalDirectory);
		if (directory.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)
			&& Path.GetFileName(directory).StartsWith("pagarte-card-tests-", StringComparison.Ordinal) && Directory.Exists(directory))
			Directory.Delete(directory, recursive: true);
	}
	private sealed class ResolverHandler(CardTestRig rig) : HttpMessageHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			if (rig.MissingProfile) return new(HttpStatusCode.NotFound);
			var body = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
			return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { clientId = body.GetProperty("subject").GetString() }) };
		}
	}
}

public sealed class CardTestCompany : ICompanyPaymentGateway
{
	public bool Pause;
	public int Calls;
	public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public async Task<PaymentOperationResult> PayServiceAsync(Payment payment, CancellationToken ct = default)
	{
		Interlocked.Increment(ref Calls);
		Entered.TrySetResult();
		if (Pause) await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
		return new(PaymentOperationOutcome.Succeeded, "synthetic-company-payment", null, null);
	}
	public Task<PaymentOperationResult> ReconcilePaymentAsync(Payment payment, CancellationToken ct = default) => Task.FromResult(new PaymentOperationResult(PaymentOperationOutcome.Succeeded, "synthetic-company-payment", null, null));
	public Task<PaymentOperationResult> RollbackAsync(Payment payment, CancellationToken ct = default) => Task.FromResult(new PaymentOperationResult(PaymentOperationOutcome.Succeeded, "synthetic-rollback", null, null));
}
