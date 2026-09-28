namespace Utilities.Money;

public readonly record struct MoneyValue
{
	private MoneyValue(
		decimal amount,
		string currency,
		int scale,
		MidpointRounding rounding)
	{
		Amount = amount;
		Currency = currency;
		Scale = scale;
		Rounding = rounding;
	}

	public decimal Amount { get; }
	public string Currency { get; }
	public int Scale { get; }
	public MidpointRounding Rounding { get; }

	public long MinorUnits => checked((long)(Amount * PowerOfTen(Scale)));

	public static MoneyValue Create(
		decimal amount,
		string currency,
		ICurrencyCatalog currencyCatalog,
		MidpointRounding rounding = MidpointRounding.ToEven)
	{
		var definition = currencyCatalog.GetRequired(currency);
		var rounded = decimal.Round(amount, definition.MinorUnitScale, rounding);
		_ = checked((long)(rounded * PowerOfTen(definition.MinorUnitScale)));
		return new MoneyValue(rounded, definition.Code, definition.MinorUnitScale, rounding);
	}

	public static MoneyValue FromMinorUnits(
		long minorUnits,
		string currency,
		ICurrencyCatalog currencyCatalog,
		MidpointRounding rounding = MidpointRounding.ToEven)
	{
		var definition = currencyCatalog.GetRequired(currency);
		var amount = minorUnits / PowerOfTen(definition.MinorUnitScale);
		return new MoneyValue(amount, definition.Code, definition.MinorUnitScale, rounding);
	}

	public static MoneyValue Sum(
		IEnumerable<MoneyValue> components,
		bool requirePositive = true)
	{
		var values = components.ToArray();
		if (values.Length == 0)
		{
			throw new MoneyValidationException("At least one monetary component is required.");
		}

		var first = values[0];
		if (values.Any(value => value.Currency != first.Currency || value.Scale != first.Scale))
		{
			throw new MoneyValidationException("All monetary components must use the same currency and scale.");
		}

		var minorUnits = values.Aggregate(0L, (total, value) => checked(total + value.MinorUnits));
		if (requirePositive && minorUnits <= 0)
		{
			throw new MoneyValidationException("The monetary total must be greater than zero.");
		}

		return new MoneyValue(
			minorUnits / PowerOfTen(first.Scale),
			first.Currency,
			first.Scale,
			first.Rounding);
	}

	private static decimal PowerOfTen(int scale)
	{
		var result = 1m;
		for (var index = 0; index < scale; index++)
		{
			result *= 10m;
		}

		return result;
	}
}

public sealed class MoneyValidationException(string message) : Exception(message);
