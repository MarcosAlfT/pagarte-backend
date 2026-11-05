using PayableServices.Application.Models;
using Utilities.Money;

namespace PayableServices.Application.Services;

internal interface IQuotePricingStrategy
{
    decimal Calculate(QuotePricingLeaf rule, MoneyValue? providerAmount, MoneyValue? basis);
}

internal static class QuotePricingStrategies
{
    public const string ProviderBase = "provider-base";
    public const string Fixed = "fixed";

    private static readonly IReadOnlyDictionary<(string Id, int Version), IQuotePricingStrategy> Registry =
        new Dictionary<(string, int), IQuotePricingStrategy>
        {
            [(ProviderBase, 1)] = new ProviderBaseStrategy(),
            [(Fixed, 1)] = new FixedStrategy(discount: false),
            [("percentage", 1)] = new PercentageStrategy(discount: false),
            [("fixed-discount", 1)] = new FixedStrategy(discount: true),
            [("percentage-discount", 1)] = new PercentageStrategy(discount: true)
        };

    public static IQuotePricingStrategy Get(QuotePricingLeaf rule) =>
        Registry.TryGetValue((rule.Strategy, rule.Version), out var strategy) ? strategy
            : throw new MoneyValidationException("The quote uses an unsupported pricing strategy.");

    private sealed class ProviderBaseStrategy : IQuotePricingStrategy
    {
        public decimal Calculate(QuotePricingLeaf rule, MoneyValue? providerAmount, MoneyValue? basis) =>
            rule.Type == "ServiceAmount" && rule.Amount is null && rule.Rate is null && basis is null &&
            providerAmount is { MinorUnits: > 0 } money ? money.Amount
                : throw new MoneyValidationException("The provider base requires a verified amount inquiry.");
    }

    private sealed class FixedStrategy(bool discount) : IQuotePricingStrategy
    {
        public decimal Calculate(QuotePricingLeaf rule, MoneyValue? providerAmount, MoneyValue? basis)
        {
            if ((rule.Type == "Discount") != discount || rule.Amount is not >= 0 || rule.Rate is not null || basis is not null)
                throw new MoneyValidationException("The fixed pricing rule has invalid inputs.");
            return discount ? -rule.Amount.Value : rule.Amount.Value;
        }
    }

    private sealed class PercentageStrategy(bool discount) : IQuotePricingStrategy
    {
        public decimal Calculate(QuotePricingLeaf rule, MoneyValue? providerAmount, MoneyValue? basis)
        {
            var supported = discount ? rule.Type == "Discount" : rule.Type is "Fee" or "Tax";
            if (!supported || rule.Amount is not null || rule.Rate is not >= 0 || basis is not { MinorUnits: >= 0 } money)
                throw new MoneyValidationException("The percentage rule requires a nonnegative rate and an explicit monetary base.");
            var amount = checked(money.Amount * rule.Rate.Value);
            return discount ? -amount : amount;
        }
    }
}
