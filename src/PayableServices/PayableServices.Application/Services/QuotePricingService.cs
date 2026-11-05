using System.Text.Json;
using System.Text.Json.Serialization;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;
using Utilities.Money;

namespace PayableServices.Application.Services;

internal sealed record PricedQuote(MoneyValue Total, IReadOnlyList<QuoteItem> Items, QuotePricingSnapshot? Snapshot = null);

public sealed class QuotePricingService(ICurrencyCatalog currencies)
{
    private static readonly JsonSerializerOptions DefinitionJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal PricedQuote Calculate(PayableService service, AmountComposition? composition, AmountInquiry? inquiry,
        IReadOnlyCollection<KeyValuePair<string, string>> references, BillIdentity? bill)
    {
        MoneyValue? providerAmount = inquiry is null ? null
            : MoneyValue.FromMinorUnits(inquiry.AmountMinorUnits!.Value, inquiry.Currency!, currencies);
        return composition?.PricingDefinitionJson is { } json
            ? PriceDefinition(Parse(json), service.Currency, providerAmount, references, bill)
            : PriceFlat(service, composition, providerAmount);
    }

    private PricedQuote PriceDefinition(QuotePricingDefinition definition, string currency, MoneyValue? providerAmount,
        IReadOnlyCollection<KeyValuePair<string, string>> references, BillIdentity? bill)
    {
        Validate(definition, currency, providerAmount is not null);
        var groups = definition.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        var rules = definition.Leaves.ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        var calculated = new Dictionary<string, (MoneyValue Value, long? BaseMinorUnits)>(StringComparer.Ordinal);
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        var membership = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<QuoteItem>();
        var evidence = new List<QuotePricingEvidence>();

        (MoneyValue Value, long? BaseMinorUnits) CalculateLeaf(string id)
        {
            if (calculated.TryGetValue(id, out var existing)) return existing;
            if (!rules.TryGetValue(id, out var rule) || !dependencies.Add(id))
                throw new MoneyValidationException("The pricing definition has a missing or cyclic monetary dependency.");

            MoneyValue? basis = rule.Bases is null ? null
                : MoneyValue.Sum(rule.Bases.Select(basisId => CalculateLeaf(basisId).Value), requirePositive: false);
            var amount = QuotePricingStrategies.Get(rule).Calculate(rule, providerAmount, basis);
            var price = (MoneyValue.Create(amount, currency, currencies), basis?.MinorUnits);
            dependencies.Remove(id);
            calculated.Add(id, price);
            return price;
        }

        QuoteBreakdownNode BuildNode(string id, int depth)
        {
            if (depth > 12 || !membership.Add(id))
                throw new MoneyValidationException("Quote groups have duplicate membership, a cycle, or too many levels.");

            if (groups.TryGetValue(id, out var group))
            {
                var children = group.Children.Select(child => BuildNode(child, depth + 1)).ToArray();
                var subtotal = children.Aggregate(0L, (sum, child) => checked(sum + child.MinorUnits!.Value));
                return new("Group", group.Label, subtotal, null, null, children);
            }

            if (!rules.TryGetValue(id, out var rule))
                throw new MoneyValidationException("A quote group references an unknown item.");

            var (value, basis) = CalculateLeaf(id);
            var source = rule.Strategy == QuotePricingStrategies.ProviderBase ? "AmountInquiry" : "ConfiguredPricing";
            var item = CreateItem(rule.Type, rule.Label, source, true, items.Count, value);
            items.Add(item);
            evidence.Add(new(item.Id, rule, basis));
            return new("Amount", item.Description, item.MinorUnits, item.Id, null, []);
        }

        var root = BuildNode(definition.Root, depth: 0);
        if (membership.Count != groups.Count + rules.Count)
            throw new MoneyValidationException("Every configured item must belong to the quote breakdown.");
        if (root.MinorUnits is not > 0)
            throw new MoneyValidationException("The monetary total must be greater than zero.");

        var referenceNodes = references.Select(reference => new QuoteBreakdownNode(
            "Reference", reference.Key, null, null, reference.Value, [])).ToList();
        if (bill is not null)
            referenceNodes.Add(new("Reference", "Billing period", null, null, bill.BillingPeriod, []));
        if (referenceNodes.Count > 0)
            root = root with { Children = [.. root.Children, new("Group", "References", null, null, null, referenceNodes)] };

        var total = MoneyValue.FromMinorUnits(root.MinorUnits.Value, currency, currencies);
        return new(total, items, new(1, root, evidence));
    }

    private static QuotePricingDefinition Parse(string json)
    {
        if (json.Length <= 65536)
        {
            try
            {
                if (JsonSerializer.Deserialize<QuotePricingDefinition>(json, DefinitionJson) is { } definition)
                    return definition;
            }
            catch (JsonException)
            {
                throw new MoneyValidationException("The quote pricing definition is invalid.");
            }
        }
        throw new MoneyValidationException("The quote pricing definition is invalid.");
    }

    private static void Validate(QuotePricingDefinition definition, string currency, bool hasProviderAmount)
    {
        if (definition.Groups is not { Count: >= 1 and <= 32 } || definition.Leaves is not { Count: >= 1 and <= 100 })
            throw new MoneyValidationException("The quote pricing definition must contain bounded groups and monetary items.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in definition.Groups)
        {
            if (group is null || !ValidId(group.Id) || !ValidLabel(group.Label) || !ids.Add(group.Id) ||
                group.Children is not { Count: >= 1 and <= 132 } || group.Children.Any(child => !ValidId(child)))
                throw new MoneyValidationException("The quote pricing definition has an invalid group.");
        }
        if (!ValidId(definition.Root) || !ids.Contains(definition.Root))
            throw new MoneyValidationException("The quote pricing definition requires a root group.");

        foreach (var leaf in definition.Leaves)
        {
            if (leaf is null || !ValidId(leaf.Id) || !ValidLabel(leaf.Label) || !ids.Add(leaf.Id) ||
                leaf.Type is not ("ServiceAmount" or "Fee" or "Tax" or "Penalty" or "Discount") ||
                leaf.Currency != currency ||
                (leaf.Bases is { } bases && (bases.Count is < 1 or > 100 || bases.Any(id => !ValidId(id)) ||
                    bases.Distinct(StringComparer.Ordinal).Count() != bases.Count)))
                throw new MoneyValidationException("The quote pricing definition has an invalid monetary item or currency.");
        }

        var baseRules = definition.Leaves.Where(leaf => leaf.Type == "ServiceAmount").ToArray();
        var expected = hasProviderAmount ? QuotePricingStrategies.ProviderBase : QuotePricingStrategies.Fixed;
        if (baseRules.Length != 1 || baseRules[0].Strategy != expected)
            throw new MoneyValidationException("The quote must define exactly one applicable service amount.");
    }

    private PricedQuote PriceFlat(PayableService service, AmountComposition? composition, MoneyValue? providerAmount)
    {
        var components = composition?.Components.OrderBy(component => component.DisplayOrder).ToArray() ?? [];
        if (components.Length == 0)
            components = [new AmountComponent
            {
                ComponentType = "ServiceAmount", Description = service.Name, Amount = service.BaseAmount,
                Currency = service.Currency, Source = "Catalogue", IsVisibleToClient = true, DisplayOrder = 1
            }];
        if (providerAmount is { } provider)
            components = components.Where(component => component.ComponentType is not ("ServiceAmount" or "Value"))
                .Prepend(new AmountComponent
                {
                    ComponentType = "ServiceAmount", Description = service.Name, Amount = provider.Amount,
                    Currency = provider.Currency, Source = "AmountInquiry", IsVisibleToClient = true
                }).ToArray();

        if (components.Any(component => component.Currency != service.Currency || string.IsNullOrWhiteSpace(component.ComponentType)))
            throw new MoneyValidationException("Every amount component requires a type and the service currency.");
        var priced = components.Select(component => (Component: component,
            Value: MoneyValue.Create(component.Amount, component.Currency, currencies))).ToArray();
        return new(MoneyValue.Sum(priced.Select(entry => entry.Value)), priced.Select(entry => CreateItem(
            entry.Component.ComponentType, entry.Component.Description, entry.Component.Source,
            entry.Component.IsVisibleToClient, entry.Component.DisplayOrder, entry.Value)).ToArray());
    }

    private static QuoteItem CreateItem(string type, string label, string source, bool visible, int order, MoneyValue money) => new()
    {
        Id = Guid.NewGuid(), ComponentType = type, Description = label, Source = source,
        IsVisibleToClient = visible, DisplayOrder = order, Amount = money.Amount,
        MinorUnits = money.MinorUnits, Currency = money.Currency
    };

    private static bool ValidId(string? id) => id is { Length: <= 64 } && !string.IsNullOrWhiteSpace(id);
    private static bool ValidLabel(string? label) => label is { Length: <= 500 } && !string.IsNullOrWhiteSpace(label);
}
