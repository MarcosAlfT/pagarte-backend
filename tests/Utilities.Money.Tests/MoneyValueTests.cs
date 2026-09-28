using Utilities.Money;

namespace Utilities.Money.Tests;

public sealed class MoneyValueTests
{
	private readonly CurrencyCatalog _catalog = new();

	[Theory]
	[InlineData("usd", "10.255", 1026, 2)]
	[InlineData("JPY", "10.5", 10, 0)]
	[InlineData("JPY", "11.5", 12, 0)]
	[InlineData("KWD", "10.1234", 10123, 3)]
	public void Create_applies_iso_scale_and_to_even_rounding(
		string currency,
		string amount,
		long expectedMinorUnits,
		int expectedScale)
	{
		var money = MoneyValue.Create(
			decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
			currency,
			_catalog);

		Assert.Equal(expectedMinorUnits, money.MinorUnits);
		Assert.Equal(expectedScale, money.Scale);
		Assert.Equal(currency.ToUpperInvariant(), money.Currency);
	}

	[Fact]
	public void Sum_uses_checked_minor_units()
	{
		var components = new[]
		{
			MoneyValue.Create(100.01m, "USD", _catalog),
			MoneyValue.Create(7.25m, "USD", _catalog),
			MoneyValue.Create(2.74m, "USD", _catalog)
		};

		var total = MoneyValue.Sum(components);

		Assert.Equal(11_000, total.MinorUnits);
		Assert.Equal(110.00m, total.Amount);
	}

	[Fact]
	public void Sum_rejects_mixed_currencies()
	{
		var components = new[]
		{
			MoneyValue.Create(1m, "USD", _catalog),
			MoneyValue.Create(1m, "CAD", _catalog)
		};

		Assert.Throws<MoneyValidationException>(() => MoneyValue.Sum(components));
	}

	[Fact]
	public void Unknown_currency_is_rejected()
	{
		Assert.Throws<MoneyValidationException>(
			() => MoneyValue.Create(1m, "XYZ", _catalog));
	}
}
