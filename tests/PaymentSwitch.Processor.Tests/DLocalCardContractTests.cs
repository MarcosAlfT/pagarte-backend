using System.Net;
using System.Text;
using System.Text.Json;
using ExternalConnections.PaymentOperators.Config;
using ExternalConnections.PaymentOperators.PaymentOperators;
using ExternalConnections.PaymentOperators.PaymentOperators.DLocal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace PaymentSwitch.Processor.Tests;

public sealed class DLocalCardContractTests
{
	private const string Card = """{"card_id":"CID-synthetic","last4":"4242","brand":"VI","expiration_month":12,"expiration_year":2040}""";
	[Fact]
	public async Task Save_uses_only_token_trusted_payer_and_the_exact_signed_body_with_distinct_credentials()
	{
		var handler = new Handler(async request =>
		{
			Assert.Equal("https://sandbox.dlocal.com/secure_cards", request.RequestUri!.ToString());
			Assert.Equal(HttpMethod.Post, request.Method);
			Assert.Equal("trans-a", request.Headers.GetValues("X-Trans-Key").Single());
			var bytes = await request.Content!.ReadAsByteArrayAsync();
			Assert.Equal(DLocalSignature.Create("login-a", "secret-a", request.Headers.GetValues("X-Date").Single(), bytes), request.Headers.GetValues("Authorization").Single());
			using var body = JsonDocument.Parse(bytes);
			Assert.Equal(new[] { "token" }, body.RootElement.GetProperty("card").EnumerateObject().Select(p => p.Name));
			Assert.Equal("trusted-client", body.RootElement.GetProperty("payer").GetProperty("user_reference").GetString());
			Assert.Equal("setup-fixture", request.Headers.GetValues("X-Idempotency-Key").Single());
			return Json(Card);
		});
		var result = await Vault(handler).SaveTokenAsync("EC", "synthetic-token", new() { ClientId = "trusted-client", Name = "Synthetic User", Email = "synthetic@example.test", Document = "synthetic-document" }, "setup-fixture", default);
		Assert.Equal(OperatorSetupOutcome.Succeeded, result.Outcome);
		Assert.Equal("CID-synthetic", result.Card!.Reference);
		Assert.Equal(1, handler.Calls);
	}

	[Theory]
	[InlineData(400, OperatorSetupOutcome.Declined)]
	[InlineData(401, OperatorSetupOutcome.Unknown)]
	[InlineData(403, OperatorSetupOutcome.Unknown)]
	[InlineData(429, OperatorSetupOutcome.Unknown)]
	[InlineData(500, OperatorSetupOutcome.Unknown)]
	public async Task Save_failure_has_no_automatic_retry(int code, OperatorSetupOutcome expected)
	{
		var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)));
		var result = await Vault(handler).SaveTokenAsync("EC", "synthetic-token", new() { ClientId = "trusted-client", Name = "Synthetic User", Email = "synthetic@example.test", Document = "synthetic-document" }, "setup-fixture", default);
		Assert.Equal(expected, result.Outcome); Assert.Equal(1, handler.Calls);
	}

	[Theory]
	[InlineData("{}")]
	[InlineData("[]")]
	[InlineData("not-json")]
	[InlineData("{\"card_id\":\"CID-synthetic\",\"last4\":\"4242\",\"brand\":\"VI\",\"expiration_month\":\"12\",\"expiration_year\":2040}")]
	[InlineData("{\"card_id\":\"CID-synthetic\",\"last4\":\"4242\",\"brand\":\"VI\",\"expiration_month\":12,\"expiration_year\":2040,\"status\":\"PENDING\"}")]
	public async Task Malformed_or_pending_evidence_does_not_make_a_card_ready(string body)
	{
		Assert.Equal(OperatorSetupOutcome.Unknown, (await Vault(new Handler(_ => Task.FromResult(Json(body)))).GetCardAsync("CID-synthetic", default)).Outcome);
	}

	[Fact]
	public async Task Card_reference_mismatch_oversized_response_and_transport_timeout_are_unknown()
	{
		Assert.Equal(OperatorSetupOutcome.Unknown, (await Vault(new Handler(_ => Task.FromResult(Json(Card.Replace("CID-synthetic", "CID-another"))))).GetCardAsync("CID-synthetic", default)).Outcome);
		Assert.Equal(OperatorSetupOutcome.Unknown, (await Vault(new Handler(_ => Task.FromResult(Json(new string('x', 40000))))).GetCardAsync("CID-synthetic", default)).Outcome);
		Assert.Equal(OperatorSetupOutcome.Unknown, (await Vault(new Handler(_ => throw new TaskCanceledException("synthetic timeout"))).GetCardAsync("CID-synthetic", default)).Outcome);
	}

	[Theory]
	[InlineData(404, OperatorCardRemovalOutcome.Removed)]
	[InlineData(200, OperatorCardRemovalOutcome.Unknown)]
	[InlineData(401, OperatorCardRemovalOutcome.Unknown)]
	public async Task Successful_delete_requires_confirmed_absence_when_response_is_not_204(int lookup, OperatorCardRemovalOutcome expected)
	{
		var handler = new Handler(request => Task.FromResult(request.Method == HttpMethod.Delete ? Json("{}") : lookup == 200 ? Json(Card) : new HttpResponseMessage((HttpStatusCode)lookup)));
		Assert.Equal(expected, await Vault(handler).RemoveAsync("CID-synthetic", "detach-fixture", default));
		Assert.Equal(2, handler.Calls);
	}

	[Fact]
	public async Task Invalid_reference_and_idempotency_key_fail_before_outbound_calls()
	{
		var handler = new Handler(_ => throw new InvalidOperationException("Unexpected call"));
		await Assert.ThrowsAsync<InvalidOperationException>(() => Vault(handler).GetCardAsync("../payments", default));
		await Assert.ThrowsAsync<InvalidOperationException>(() => Vault(handler).RemoveAsync("CID-synthetic", new string('x', 43), default));
		Assert.Equal(0, handler.Calls);
	}

	[Fact]
	public async Task Factory_routes_two_real_adapter_accounts_without_default_selection_or_global_credentials()
	{
		var values = new Dictionary<string, string?>();
		foreach (var id in new[] { "a", "b" })
		{
			values[$"PaymentOperators:Integrations:{id}:Adapter"] = "DLocal";
			values[$"PaymentOperators:Integrations:{id}:Account"] = "merchant-" + id;
			values[$"PaymentOperators:Integrations:{id}:Environment"] = "Sandbox";
			values[$"PaymentOperators:Integrations:{id}:Version"] = "1";
			values[$"PaymentOperators:Integrations:{id}:CredentialReference"] = "credential-" + id;
			values[$"PaymentOperatorCredentials:credential-{id}:Account"] = "merchant-" + id;
			values[$"PaymentOperatorCredentials:credential-{id}:Environment"] = "Sandbox";
			values[$"PaymentOperatorCredentials:credential-{id}:XLogin"] = "login-" + id;
			values[$"PaymentOperatorCredentials:credential-{id}:XTransKey"] = "trans-" + id;
			values[$"PaymentOperatorCredentials:credential-{id}:SecretKey"] = "secret-" + id;
		}
		var settings = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
		var seen = new List<string>();
		var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(settings).AddSingleton<IHostEnvironment>(new EnvironmentStub());
		services.AddExternalConnections(settings);
		services.AddHttpClient("dlocal").ConfigurePrimaryHttpMessageHandler(() => new Handler(request => { seen.Add(request.Headers.GetValues("X-Login").Single()); return Task.FromResult(Json(Card)); }));
		await using var provider = services.BuildServiceProvider();
		provider.GetRequiredService<PaymentOperatorRouteGuard>().Initialize(new[] { "a", "b" }.Select(id => new OperatorCardIdentity(id, "DLocal", "merchant-" + id, "Sandbox", "1", "credential-" + id)));
		using var scope = provider.CreateScope();
		var factory = scope.ServiceProvider.GetRequiredService<IPaymentOperatorAdapterFactory>();
		Assert.True((await factory.GetRequiredAdapter("a").CheckCardAsync("CID-synthetic")).Ready);
		Assert.True((await factory.GetRequiredAdapter("b").CheckCardAsync("CID-synthetic")).Ready);
		Assert.Equal(new[] { "login-a", "login-b" }, seen);
		Assert.False(factory.GetRequiredAdapter("a") is ITokenizedCardSetupAdapter);
		settings["PaymentOperators:Integrations:a:CredentialReference"] = "credential-b";
		Assert.Throws<InvalidOperationException>(() => factory.GetRequiredAdapter("a"));
	}

	[Fact]
	public void Credentials_reject_wrong_account_environment_and_legacy_secret_conflation()
	{
		var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["PaymentOperator:ApiUrl"] = "https://operator.test", ["PaymentOperator:ApiKey"] = "legacy", ["PaymentOperator:ApiSecret"] = "legacy-conflated" }).Build();
		Assert.Throws<InvalidOperationException>(() => DLocalConnection.Resolve(settings));
		Assert.Throws<InvalidOperationException>(() => DLocalConnection.Resolve(settings, new("dlocal-a", "DLocal", "merchant-a", "Sandbox", "1", "missing")));
	}

	[Fact]
	public void Notification_signature_checks_exact_raw_bytes_and_does_not_accept_tampering()
	{
		const string timestamp = "2026-11-03T12:00:00.000Z";
		var body = Encoding.UTF8.GetBytes("{\"id\":\"synthetic-payment\"}");
		const string authorization = "V2-HMAC-SHA256, Signature: 7918ca5f56b8fdeec03c70776ccb882741e80dfd790e363fbccc81885abd3782";
		Assert.True(DLocalSignature.Verify("login-a", "secret-a", timestamp, authorization, body));
		Assert.False(DLocalSignature.Verify("login-b", "secret-a", timestamp, authorization, body));
		Assert.False(DLocalSignature.Verify("login-a", "secret-a", timestamp, authorization, body.Concat(new byte[] { 32 }).ToArray()));
		Assert.False(DLocalSignature.Verify("login-a", "secret-a", timestamp, "bad", body));
	}

	[Theory]
	[InlineData("PAID", PaymentOperatorOutcome.Succeeded)]
	[InlineData("AUTHORIZED", PaymentOperatorOutcome.Unknown)]
	[InlineData("PENDING", PaymentOperatorOutcome.Unknown)]
	[InlineData("UNRECOGNIZED", PaymentOperatorOutcome.Unknown)]
	[InlineData("REJECTED", PaymentOperatorOutcome.DefinitiveDecline)]
	public async Task Existing_payment_status_mapping_never_treats_an_unconfirmed_charge_as_paid(string status, PaymentOperatorOutcome expected)
	{
		var adapter = Legacy(new Handler(_ => Task.FromResult(Json(JsonSerializer.Serialize(new { id = "synthetic-payment", status })))));
		Assert.Equal(expected, (await adapter.GetChargeStatusAsync("synthetic-payment")).Outcome);
	}
	[Theory]
	[InlineData("SUCCESS", PaymentOperatorOutcome.Succeeded)]
	[InlineData("PENDING", PaymentOperatorOutcome.Unknown)]
	[InlineData("UNRECOGNIZED", PaymentOperatorOutcome.Unknown)]
	public async Task Existing_refund_mapping_requires_explicit_success(string status, PaymentOperatorOutcome expected)
	{
		var adapter = Legacy(new Handler(_ => Task.FromResult(Json(JsonSerializer.Serialize(new { id = "synthetic-refund", status })))));
		Assert.Equal(expected, (await adapter.RefundAsync("synthetic-payment", 1m, "USD", "test", "refund-fixture")).Outcome);
	}
	[Fact]
	public async Task Payment_status_lookup_rejects_evidence_for_another_reference()
	{
		var adapter = Legacy(new Handler(_ => Task.FromResult(Json("""{"id":"another-payment","status":"PAID"}"""))));
		Assert.Equal(PaymentOperatorOutcome.Unknown, (await adapter.GetChargeStatusAsync("synthetic-payment")).Outcome);
	}
	private static DLocalPaymentOperatorAdapter Legacy(Handler handler)
	{
		var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["PaymentOperator:ApiUrl"] = "https://operator.test", ["PaymentOperator:XLogin"] = "login-a",
			["PaymentOperator:XTransKey"] = "trans-a", ["PaymentOperator:SecretKey"] = "secret-a" }).Build();
		return new(new Factory(handler), settings, Microsoft.Extensions.Logging.Abstractions.NullLogger<DLocalPaymentOperatorAdapter>.Instance,
			new PaymentOperatorRegistry(settings, new EnvironmentStub()));
	}

	private static DLocalCardVaultClient Vault(Handler handler) => new(new(new Factory(handler), new DLocalConnection {
		BaseUri = new("https://sandbox.dlocal.com"), XLogin = "login-a", XTransKey = "trans-a", SecretKey = "secret-a" }));
	private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
	private sealed class Factory(Handler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, disposeHandler: false); }
	private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
	{
		public int Calls;
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return send(request); }
	}
	private sealed class EnvironmentStub : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = "Development";
		public string ApplicationName { get; set; } = "Tests";
		public string ContentRootPath { get; set; } = "";
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
