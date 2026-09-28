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
		var configuredKey = configuration["WorkloadAuth:ApiKey"];
		if (string.IsNullOrWhiteSpace(configuredKey))
		{
			return Task.FromResult(AuthenticateResult.Fail("Workload authentication is not configured."));
		}

		var suppliedKey = Request.Headers[HeaderName].ToString();
		if (string.IsNullOrWhiteSpace(suppliedKey)
			|| !CryptographicOperations.FixedTimeEquals(
				Encoding.UTF8.GetBytes(configuredKey),
				Encoding.UTF8.GetBytes(suppliedKey)))
		{
			return Task.FromResult(AuthenticateResult.Fail("Invalid workload credentials."));
		}

		var identity = new ClaimsIdentity(
			[new Claim(ClaimTypes.NameIdentifier, "payable-services")],
			SchemeName);
		return Task.FromResult(AuthenticateResult.Success(
			new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
	}
}
