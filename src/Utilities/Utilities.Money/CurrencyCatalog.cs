using System.Collections.Frozen;

namespace Utilities.Money;

public sealed record CurrencyDefinition(string Code, int MinorUnitScale);

public interface ICurrencyCatalog
{
	CurrencyDefinition GetRequired(string currencyCode);
}

public sealed class CurrencyCatalog : ICurrencyCatalog
{
	private static readonly FrozenDictionary<string, CurrencyDefinition> Definitions =
		new[]
		{
			Define("ARS", 2), Define("BHD", 3), Define("BOB", 2),
			Define("BRL", 2), Define("CAD", 2), Define("CLP", 0),
			Define("COP", 2), Define("EUR", 2), Define("GBP", 2),
			Define("JPY", 0), Define("KWD", 3), Define("MXN", 2),
			Define("OMR", 3), Define("PEN", 2), Define("PYG", 0),
			Define("TND", 3), Define("USD", 2), Define("UYU", 2)
		}.ToFrozenDictionary(x => x.Code, StringComparer.Ordinal);

	public CurrencyDefinition GetRequired(string currencyCode)
	{
		var normalized = Normalize(currencyCode);
		return Definitions.TryGetValue(normalized, out var definition)
			? definition
			: throw new MoneyValidationException(
				$"Currency '{normalized}' is not supported by the configured ISO 4217 catalogue.");
	}

	public static string Normalize(string currencyCode)
	{
		if (string.IsNullOrWhiteSpace(currencyCode))
		{
			throw new MoneyValidationException("Currency is required.");
		}

		var normalized = currencyCode.Trim().ToUpperInvariant();
		if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
		{
			throw new MoneyValidationException("Currency must be a three-letter ISO 4217 code.");
		}

		return normalized;
	}

	private static CurrencyDefinition Define(string code, int scale) => new(code, scale);
}
