using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace PaymentSwitch.Processor.Security;

public sealed class WorkloadApiKeyAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder,
	IConfiguration configuration)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "WorkloadApiKey";
	private const string HeaderName = "x-workload-api-key";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var suppliedKey = Request.Headers[HeaderName].Count == 1 ? Request.Headers[HeaderName].ToString() : "";
		var generalKey = configuration["WorkloadAuth:ApiKey"];
		var operationsKey = configuration["CardOperationsAuth:ApiKey"];
		var recoveryKey = configuration["PaymentRecoveryAuth:ApiKey"];
		var operations = !string.Equals(generalKey, operationsKey, StringComparison.Ordinal) && Matches(operationsKey, suppliedKey);
		var recovery = recoveryKey != generalKey && recoveryKey != operationsKey && Matches(recoveryKey, suppliedKey);
		if (!operations && !recovery && !Matches(generalKey, suppliedKey))
			return Task.FromResult(AuthenticateResult.Fail("Invalid workload credentials."));
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, recovery ? "pagarte-payment-recovery" : operations ? "pagarte-card-operations" : "pagarte-workload")
		};
		if (operations) claims.Add(new("card-operations", "true"));
		if (recovery) claims.Add(new("payment-recovery", "true"));
		var identity = new ClaimsIdentity(claims, SchemeName);
		return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
	}
	private static bool Matches(string? configured, string supplied) => !string.IsNullOrWhiteSpace(configured) && !string.IsNullOrWhiteSpace(supplied)
		&& CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configured), Encoding.UTF8.GetBytes(supplied));
}
