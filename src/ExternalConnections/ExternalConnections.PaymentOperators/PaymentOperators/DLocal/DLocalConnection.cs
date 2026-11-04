using ExternalConnections.PaymentOperators.Config;
using Microsoft.Extensions.Configuration;

namespace ExternalConnections.PaymentOperators.PaymentOperators.DLocal;

// Server-only credentials. Do not expose this object through public contracts or logging.
public sealed class DLocalConnection
{
	public required Uri BaseUri { get; init; }
	public required string XLogin { get; init; }
	public required string XTransKey { get; init; }
	public required string SecretKey { get; init; }
	public string? PublicKey { get; init; }
	public override string ToString() => "dLocal connection [credentials redacted]";

	public static DLocalConnection Resolve(IConfiguration configuration, OperatorCardIdentity? identity = null)
	{
		if (identity is null)
			return Create(configuration["PaymentOperator:ApiUrl"], configuration["PaymentOperator:XLogin"] ?? configuration["PaymentOperator:ApiKey"],
				configuration["PaymentOperator:XTransKey"], configuration["PaymentOperator:SecretKey"], null);
		var reference = identity.CredentialReference;
		if (identity.Provider != "DLocal" || string.IsNullOrWhiteSpace(reference) || reference.Length > 100)
			throw new InvalidOperationException("Configure a dLocal credential reference for this integration.");
		var credentials = configuration.GetSection($"PaymentOperatorCredentials:{reference}");
		if (credentials["Account"] != identity.Account || credentials["Environment"] != identity.Environment)
			throw new InvalidOperationException("dLocal credentials do not match the retained account/environment.");
		var endpoint = identity.Environment switch
		{
			"Sandbox" => "https://sandbox.dlocal.com/",
			"Production" => "https://api.dlocal.com/",
			_ => throw new InvalidOperationException("dLocal requires an explicit Sandbox or Production environment.")
		};
		return Create(endpoint, credentials["XLogin"], credentials["XTransKey"], credentials["SecretKey"], credentials["PublicKey"]);
	}

	private static DLocalConnection Create(string? url, string? login, string? transKey, string? secret, string? publicKey)
	{
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
			|| !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
			|| new[] { login, transKey, secret }.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 1000 || v.Any(char.IsControl)))
			throw new InvalidOperationException("Configure HTTPS and separate dLocal XLogin, XTransKey, and SecretKey credentials.");
		return new() { BaseUri = uri, XLogin = login!, XTransKey = transKey!, SecretKey = secret!, PublicKey = publicKey };
	}
}
