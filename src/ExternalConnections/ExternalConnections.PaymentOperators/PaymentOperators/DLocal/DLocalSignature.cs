using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ExternalConnections.PaymentOperators.PaymentOperators.DLocal;

public static class DLocalSignature
{
	private const string Prefix = "V2-HMAC-SHA256, Signature: ";
	public static string Create(string login, string secret, string timestamp, ReadOnlySpan<byte> body)
	{
		var prefix = Encoding.UTF8.GetBytes(login + timestamp);
		var data = new byte[prefix.Length + body.Length];
		prefix.CopyTo(data, 0); body.CopyTo(data.AsSpan(prefix.Length));
		return Prefix + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), data)).ToLowerInvariant();
	}

	// Authenticity only. The caller must correlate and durably deduplicate supported event IDs.
	// No setup-event schema or customer ownership is inferred from a valid payment signature.
	public static bool Verify(string login, string secret, string timestamp, string authorization, byte[] body)
	{
		if (body.Length > 8192 || timestamp.Length > 50 || authorization.Length != Prefix.Length + 64
			|| !authorization.StartsWith(Prefix, StringComparison.Ordinal)
			|| !DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
			|| date.Offset != TimeSpan.Zero) return false;
		try
		{
			var expected = Create(login, secret, timestamp, body)[Prefix.Length..];
			return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(authorization[Prefix.Length..]));
		}
		catch (FormatException) { return false; }
	}
}
