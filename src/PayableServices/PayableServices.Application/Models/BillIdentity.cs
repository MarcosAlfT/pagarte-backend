using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace PayableServices.Application.Models;

public sealed record BillIdentity(string StrategyId, int StrategyVersion,
    string Company, string Service, string AccountNumber, string BillingPeriod, string Key);

internal static class BillIdentityStrategyRegistry
{
    private static readonly IReadOnlyDictionary<(string Provider, string Service),
        Func<string, IReadOnlyDictionary<string, string>, string?, BillIdentity?>> Strategies =
        new Dictionary<(string, string), Func<string, IReadOnlyDictionary<string, string>, string?, BillIdentity?>>
        {
            [("MOCKCOMPANY", "ELEC-EC")] = CompanyAccountPeriod,
            [("MOCKCOMPANY", "WATER-EC")] = CompanyAccountPeriod,
            [("MOCKCOMPANY", "NET-US")] = CompanyAccountPeriod
        };

    public static BillIdentity? Select(string provider, string service,
        IReadOnlyDictionary<string, string> references, string? billingPeriod) =>
        Strategies.TryGetValue((provider.ToUpperInvariant(), service), out var strategy)
            ? strategy(service, references, billingPeriod) : null;

    private static BillIdentity? CompanyAccountPeriod(string service,
        IReadOnlyDictionary<string, string> references, string? billingPeriod)
    {
        if (!references.TryGetValue("accountNumber", out var account) ||
            account.Length is < 8 or > 20 || account.Any(c => !char.IsAsciiDigit(c)) ||
            billingPeriod is not { Length: 7 } ||
            !DateOnly.TryParseExact(billingPeriod + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            return null;

        const string company = "mock-company";
        // Version describes selection. The key identifies the obligation across compatible versions.
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new[] { company, service, account, billingPeriod });
        var key = Convert.ToHexStringLower(SHA256.HashData(canonical));
        return new("company-service-account-period", 1, company, service, account, billingPeriod, key);
    }
}
