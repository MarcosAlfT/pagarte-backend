using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using PaymentSwitch.Processor.Application.Abstractions;

namespace PaymentSwitch.Processor.Infrastructure;

public sealed class RegistrationTokenProtector(IDataProtectionProvider provider, IConfiguration configuration) : IRegistrationTokenProtector
{
	private readonly IDataProtector _protector = provider.CreateProtector("Pagarte.CardSetup.TemporaryToken.v1");
	public void EnsureConfigured() => _ = GetKey();
	private string GetKey()
	{
		var key = configuration["PaymentMethods:TokenDigestKey"];
		if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
			throw new InvalidOperationException("PaymentMethods:TokenDigestKey requires at least 32 bytes.");
		return key;
	}
	public string Digest(string scope, string token)
	{
		return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(GetKey()), Encoding.UTF8.GetBytes($"card-setup-v1\n{scope}\n{token}")));
	}
	public string Protect(string token) => _protector.Protect(token);
	public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}
