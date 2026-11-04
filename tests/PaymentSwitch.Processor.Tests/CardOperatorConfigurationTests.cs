using ExternalConnections.PaymentOperators.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExternalConnections.PaymentOperators.PaymentOperators;

namespace PaymentSwitch.Processor.Tests;

public sealed class CardOperatorConfigurationTests
{
	[Fact]
	public void Production_rejects_mock_for_new_registration_and_existing_operations()
	{
		var settings = Settings();
		var registry = new PaymentOperatorRegistry(settings, new EnvironmentStub { EnvironmentName = Environments.Production });
		Assert.Throws<InvalidOperationException>(() => registry.SelectForRegistration());
		Assert.Throws<InvalidOperationException>(() => registry.ResolveAdapter("mock-a"));
		Assert.Throws<InvalidOperationException>(() => registry.ResolveAdapter("Mock"));
	}

	[Fact]
	public void Disabling_new_registration_preserves_original_operation_routing()
	{
		var settings = Settings();
		settings["PaymentOperators:Integrations:mock-a:RegistrationEnabled"] = "false";
		var registry = new PaymentOperatorRegistry(settings, new EnvironmentStub());
		Assert.Throws<InvalidOperationException>(() => registry.SelectForRegistration());
		Assert.Equal("Mock", registry.ResolveAdapter("mock-a"));
	}

	[Fact]
	public void Missing_market_capabilities_prevent_enabling_a_registration_integration()
	{
		var settings = Settings();
		settings["PaymentOperators:Integrations:mock-a:SupportedCountries:0"] = null;
		var registry = new PaymentOperatorRegistry(settings, new EnvironmentStub());
		Assert.Throws<InvalidOperationException>(() => registry.SelectForRegistration());
	}

	[Fact]
	public async Task Operator_http_logs_do_not_disclose_saved_card_references_in_request_paths()
	{
		var settings = Settings();
		settings["PaymentOperator:ApiUrl"] = "https://operator.test";
		settings["PaymentOperator:ApiKey"] = "synthetic-api-key";
		settings["PaymentOperator:XTransKey"] = "synthetic-trans-key";
		settings["PaymentOperator:SecretKey"] = "synthetic-signing-key";
		var logs = new CollectedLogs();
		var services = new ServiceCollection();
		services.AddSingleton<IHostEnvironment>(new EnvironmentStub());
		services.AddSingleton<IConfiguration>(settings);
		services.AddLogging(builder => builder.AddProvider(logs));
		services.AddExternalConnections(settings);
		services.AddHttpClient("dlocal").ConfigurePrimaryHttpMessageHandler(() => new CardStatusHandler());
		await using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();
		var result = await scope.ServiceProvider.GetRequiredService<DLocalPaymentOperatorAdapter>().CheckCardAsync("synthetic-secret-card-reference");
		Assert.True(result.Ready);
		Assert.DoesNotContain(logs.Messages, message => message.Contains("synthetic-secret-card-reference", StringComparison.Ordinal));
	}

	private sealed class CardStatusHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
		{
			Assert.EndsWith("/cards/synthetic-secret-card-reference", request.RequestUri!.AbsolutePath);
			return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("""{"card_id":"synthetic-secret-card-reference","last4":"4242","brand":"VI","expiration_month":12,"expiration_year":2040}""") });
		}
	}
	private sealed class CollectedLogs : ILoggerProvider
	{
		public readonly System.Collections.Concurrent.ConcurrentBag<string> Messages = [];
		public ILogger CreateLogger(string categoryName) => new Logger(Messages);
		public void Dispose() { }
		private sealed class Logger(System.Collections.Concurrent.ConcurrentBag<string> messages) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? error, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, error));
		}
	}

	private static IConfigurationRoot Settings() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
	{
		["PaymentOperators:DefaultIntegration"] = "mock-a",
		["PaymentOperators:Integrations:mock-a:Adapter"] = "Mock",
		["PaymentOperators:Integrations:mock-a:Account"] = "synthetic",
		["PaymentOperators:Integrations:mock-a:Environment"] = "Development",
		["PaymentOperators:Integrations:mock-a:Version"] = "1",
		["PaymentOperators:Integrations:mock-a:RegistrationEnabled"] = "true",
		["PaymentOperators:Integrations:mock-a:SupportedCountries:0"] = "EC",
		["PaymentOperators:Integrations:mock-a:SupportedCurrencies:0"] = "USD"
	}).Build();

	private sealed class EnvironmentStub : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Development;
		public string ApplicationName { get; set; } = "PaymentSwitch.Processor.Tests";
		public string ContentRootPath { get; set; } = "";
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
