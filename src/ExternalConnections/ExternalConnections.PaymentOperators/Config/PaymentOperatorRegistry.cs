using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ExternalConnections.PaymentOperators.Config;

public sealed class OperatorIntegration
{
	public string Adapter { get; set; } = "";
	public string Account { get; set; } = "";
	public string Environment { get; set; } = "";
	public string Version { get; set; } = "";
	public string CredentialReference { get; set; } = "";
	public string CallbackKeyReference { get; set; } = "";
	public bool RegistrationEnabled { get; set; }
	public bool CardRemovalEnabled { get; set; }
	public bool RefundsSurviveDetachment { get; set; }
	public string[] SupportedCountries { get; set; } = [];
	public string[] SupportedCurrencies { get; set; } = [];
}

public sealed class PaymentOperatorRegistry(IConfiguration configuration, IHostEnvironment environment)
{
	public IReadOnlyList<(string Id, OperatorIntegration Integration)> GetConfigured() => configuration.GetSection("PaymentOperators:Integrations")
		.GetChildren().Select(section => (section.Key, GetRequired(section.Key))).ToArray();

	public (string Id, OperatorIntegration Integration) SelectForRegistration()
	{
		var id = configuration["PaymentOperators:DefaultIntegration"];
		if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("No card-registration integration is configured.");
		if (id.Equals("Mock", StringComparison.OrdinalIgnoreCase) || id.Equals("DLocal", StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("Use a unique integration ID; legacy provider codes cannot identify new registration accounts.");
		var integration = GetRequired(id);
		if (integration.SupportedCountries.Length == 0 || integration.SupportedCurrencies.Length == 0
			|| integration.SupportedCountries.Any(c => string.IsNullOrWhiteSpace(c) || c.Length != 2 || !c.All(char.IsAsciiLetter))
			|| integration.SupportedCurrencies.Any(c => string.IsNullOrWhiteSpace(c) || c.Length != 3 || !c.All(char.IsAsciiLetter)))
			throw new InvalidOperationException("The integration must declare supported markets and currencies.");
		if (!integration.RegistrationEnabled) throw new InvalidOperationException("The selected integration is closed for registration.");
		return (id, integration);
	}

	public OperatorIntegration GetRequired(string id)
	{
		var integration = configuration.GetSection($"PaymentOperators:Integrations:{id}").Get<OperatorIntegration>();
		if (integration is null || string.IsNullOrWhiteSpace(integration.Adapter)
			|| string.IsNullOrWhiteSpace(integration.Account) || string.IsNullOrWhiteSpace(integration.Environment)
			|| string.IsNullOrWhiteSpace(integration.Version) || id.Length > 100 || integration.Adapter.Length > 100
			|| integration.CredentialReference.Length > 100 || integration.Account.Length > 100 || integration.Environment.Length > 30 || integration.Version.Length > 50)
			throw new InvalidOperationException("The payment integration is missing required configuration.");
		EnsureAllowed(integration.Adapter);
		return integration;
	}

	public string ResolveAdapter(string id)
	{
		// Retain the original route for legacy instruments and outstanding payments.
		var adapter = id.Equals("Mock", StringComparison.OrdinalIgnoreCase) || id.Equals("DLocal", StringComparison.OrdinalIgnoreCase)
			? id : GetRequired(id).Adapter;
		EnsureAllowed(adapter);
		return adapter;
	}

	private void EnsureAllowed(string adapter)
	{
		if (adapter.Equals("Mock", StringComparison.OrdinalIgnoreCase) && !environment.IsDevelopment())
			throw new InvalidOperationException("The mock payment operator is available only in Development/tests.");
	}
}
