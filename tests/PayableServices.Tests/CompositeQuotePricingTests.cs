using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;

namespace PayableServices.Tests;

public sealed class CompositeQuotePricingTests
{
    [Fact]
    public async Task Pricing_snapshot_contains_nested_subtotals_and_auditable_rules_without_repricing_after_configuration_changes()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        await SyntheticQuotePricing.StoreAsync(rig, SyntheticQuotePricing.Definition());
        var inquiryId = await InquireAsync(rig, client);
        var quote = await TestRig.Data(await rig.Quote(client, inquiryId));
        Assert.Equal(SyntheticQuotePricing.Total, quote.GetProperty("totalMinorUnits").GetInt64());
        var lines = quote.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new long[] { 12845, 200, 1284, 716, 6, -25, -148 },
            lines.Select(line => line.GetProperty("minorUnits").GetInt64()).ToArray());
        Assert.All(lines, line => Assert.True(line.GetProperty("isVisibleToClient").GetBoolean()));
        var breakdown = quote.GetProperty("breakdown");
        Assert.Equal("Group", breakdown.GetProperty("kind").GetString());
        Assert.Equal(SyntheticQuotePricing.Total, breakdown.GetProperty("minorUnits").GetInt64());
        var adjustments = breakdown.GetProperty("children")[1];
        Assert.Equal(2033L, adjustments.GetProperty("minorUnits").GetInt64());
        Assert.Equal(1484L, adjustments.GetProperty("children")[0].GetProperty("minorUnits").GetInt64());
        Assert.Equal(lines[1].GetProperty("id").GetGuid(),
            adjustments.GetProperty("children")[0].GetProperty("children")[0].GetProperty("itemId").GetGuid());
        var references = breakdown.GetProperty("children")[2];
        Assert.Equal(JsonValueKind.Null, references.GetProperty("minorUnits").ValueKind);
        Assert.Contains(references.GetProperty("children").EnumerateArray(),
            node => node.GetProperty("referenceValue").GetString() == "2026-10");
        Assert.DoesNotContain("percentage-discount", quote.GetRawText());

        await rig.InDb(async db =>
        {
            var stored = await db.Quotes.SingleAsync();
            using var evidenceDocument = JsonDocument.Parse(stored.PricingSnapshotJson!);
            var evidence = evidenceDocument.RootElement.GetProperty("Evidence").EnumerateArray().ToArray();
            var tax = evidence.Single(row => row.GetProperty("Rule").GetProperty("Id").GetString() == "tax");
            Assert.Equal(14329L, tax.GetProperty("BaseMinorUnits").GetInt64());
            Assert.Equal(0.05m, tax.GetProperty("Rule").GetProperty("Rate").GetDecimal());
            Assert.Equal(1, tax.GetProperty("Rule").GetProperty("Version").GetInt32());
        });

        await SyntheticQuotePricing.StoreAsync(rig, SyntheticQuotePricing.ChangedAllocation());
        var quoteId = quote.GetProperty("id").GetGuid();
        var reread = await TestRig.Data(await client.GetAsync($"/api/quotes/{quoteId}"));
        Assert.Equal(breakdown.GetRawText(), reread.GetProperty("breakdown").GetRawText());
        var replay = await TestRig.Data(await rig.Quote(client, inquiryId));
        Assert.Equal(breakdown.GetRawText(), replay.GetProperty("breakdown").GetRawText());

        var next = await TestRig.Data(await rig.Quote(client, await InquireAsync(rig, client)));
        Assert.Equal(SyntheticQuotePricing.Total, next.GetProperty("totalMinorUnits").GetInt64());
        Assert.Equal(201L, next.GetProperty("items")[1].GetProperty("minorUnits").GetInt64());
        Assert.Equal(5L, next.GetProperty("items")[4].GetProperty("minorUnits").GetInt64());
    }

    [Fact]
    public async Task Invalid_composites_and_rules_fail_without_consuming_the_inquiry()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var inquiryId = await InquireAsync(rig, client);
        var good = SyntheticQuotePricing.Definition();
        var invalid = new (string Name, QuotePricingDefinition Definition)[]
        {
            ("cycle", SyntheticQuotePricing.Group(good, "adjustments", group => group with { Children = ["root"] })),
            ("duplicate membership", SyntheticQuotePricing.Group(good, "fees", group => group with { Children = [.. group.Children, "base"] })),
            ("missing membership", SyntheticQuotePricing.Group(good, "discounts", group => group with { Children = ["fixed-discount"] })),
            ("unknown child", SyntheticQuotePricing.Group(good, "fees", group => group with { Children = ["unknown"] })),
            ("missing percentage base", SyntheticQuotePricing.Leaf(good, "tax", leaf => leaf with { Bases = null })),
            ("group as basis", SyntheticQuotePricing.Leaf(good, "tax", leaf => leaf with { Bases = ["fees"] })),
            ("duplicate basis", SyntheticQuotePricing.Leaf(good, "tax", leaf => leaf with { Bases = ["base", "base"] })),
            ("dependency cycle", SyntheticQuotePricing.Leaf(good, "percentage-fee", leaf => leaf with { Bases = ["tax"] })),
            ("mixed currency", SyntheticQuotePricing.Leaf(good, "tax", leaf => leaf with { Currency = "EUR" })),
            ("unknown strategy version", SyntheticQuotePricing.Leaf(good, "tax", leaf => leaf with { Version = 99 })),
            ("invalid discount input", SyntheticQuotePricing.Leaf(good, "fixed-discount", leaf => leaf with { Amount = -0.25m })),
            ("nonpositive total", SyntheticQuotePricing.Leaf(good, "fixed-discount", leaf => leaf with { Amount = 200m })),
            ("overflow", SyntheticQuotePricing.Leaf(good, "penalty", leaf => leaf with { Amount = decimal.MaxValue })),
            ("incorrect bill base", SyntheticQuotePricing.Leaf(good, "base", leaf => leaf with { Strategy = "fixed", Amount = 25m }))
        };
        foreach (var (name, definition) in invalid)
        {
            await SyntheticQuotePricing.StoreAsync(rig, definition);
            var result = await rig.Quote(client, inquiryId);
            Assert.True(result.StatusCode == HttpStatusCode.BadRequest, $"Expected rejection for {name}.");
        }

        await rig.InDb(async db =>
        {
            Assert.Empty(await db.Quotes.ToArrayAsync());
            Assert.Null((await db.AmountInquiries.SingleAsync()).QuoteId);
        });
        await SyntheticQuotePricing.StoreAsync(rig, good);
        Assert.Equal(SyntheticQuotePricing.Total,
            (await TestRig.Data(await rig.Quote(client, inquiryId))).GetProperty("totalMinorUnits").GetInt64());
    }

    [Theory]
    [InlineData("JPY", 0, 2)]
    [InlineData("KWD", 3, 3030)]
    public async Task Configured_price_uses_shared_currency_scale_and_keeps_existing_flat_quotes(
        string currency, int scale, long total)
    {
        await using var rig = await TestRig.CreateAsync(activate: false);
        using var client = rig.Client();
        var serviceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        async Task<JsonElement> CreateQuoteAsync(string currencyCode) => await TestRig.Data(await client.PostAsJsonAsync("/api/quotes",
            new { serviceId, currency = currencyCode, referenceValues = new { accountNumber = "00123456" } }));
        var flat = await CreateQuoteAsync("USD");
        Assert.Equal(JsonValueKind.Null, flat.GetProperty("breakdown").ValueKind);
        await rig.InDb(async db =>
        {
            (await db.PayableServices.SingleAsync(service => service.Id == serviceId)).Currency = currency;
            (await db.Countries.SingleAsync(country => country.Id == TestRig.CountryId)).Currency = currency;
            await db.SaveChangesAsync();
        });
        await SyntheticQuotePricing.StoreAsync(rig, new("root", [new("root", "Total", ["base", "fee"])],
            [new("base", "ServiceAmount", "Base", currency, "fixed", 1, Amount: 2.5m),
             new("fee", "Fee", "Fee", currency, "percentage", 1, Rate: 0.212m, Bases: ["base"])]), serviceId);

        var composite = await CreateQuoteAsync(currency);
        Assert.Equal(scale, composite.GetProperty("currencyScale").GetInt32());
        Assert.Equal(total, composite.GetProperty("totalMinorUnits").GetInt64());
        var old = await TestRig.Data(await client.GetAsync($"/api/quotes/{flat.GetProperty("id").GetGuid()}"));
        Assert.Equal(flat.GetProperty("totalMinorUnits").GetInt64(), old.GetProperty("totalMinorUnits").GetInt64());
        Assert.Equal(JsonValueKind.Null, old.GetProperty("breakdown").ValueKind);
    }

    private static async Task<Guid> InquireAsync(TestRig rig, HttpClient client) =>
        (await TestRig.Data(await rig.Inquire(client))).GetProperty("id").GetGuid();
}

internal static class SyntheticQuotePricing
{
    public const long Total = 14878;

    public static QuotePricingDefinition Definition() => new("root",
        [new("root", "Quote total", ["base", "adjustments"]),
         new("adjustments", "Adjustments", ["fees", "tax", "penalty", "discounts"]),
         new("fees", "Fees", ["fixed-fee", "percentage-fee"]),
         new("discounts", "Discounts", ["fixed-discount", "percentage-discount"])],
        [new("base", "ServiceAmount", "Provider bill", "USD", "provider-base", 1),
         new("fixed-fee", "Fee", "Fixed fee", "USD", "fixed", 1, Amount: 2m),
         new("percentage-fee", "Fee", "Percentage fee", "USD", "percentage", 1, Rate: 0.10m, Bases: ["base"]),
         new("tax", "Tax", "Tax", "USD", "percentage", 1, Rate: 0.05m, Bases: ["base", "fixed-fee", "percentage-fee"]),
         new("penalty", "Penalty", "Penalty", "USD", "fixed", 1, Amount: 0.055m),
         new("fixed-discount", "Discount", "Fixed discount", "USD", "fixed-discount", 1, Amount: 0.25m),
         new("percentage-discount", "Discount", "Percentage discount", "USD", "percentage-discount", 1, Rate: 0.10m,
             Bases: ["fixed-fee", "percentage-fee"])]);

    public static QuotePricingDefinition ChangedAllocation() =>
        Leaf(Leaf(Definition(), "fixed-fee", leaf => leaf with { Amount = 2.01m }), "penalty", leaf => leaf with { Amount = 0.05m });

    public static QuotePricingDefinition Leaf(QuotePricingDefinition definition, string id, Func<QuotePricingLeaf, QuotePricingLeaf> edit) =>
        definition with { Leaves = definition.Leaves.Select(leaf => leaf.Id == id ? edit(leaf) : leaf).ToArray() };

    public static QuotePricingDefinition Group(QuotePricingDefinition definition, string id, Func<QuotePricingGroup, QuotePricingGroup> edit) =>
        definition with { Groups = definition.Groups.Select(group => group.Id == id ? edit(group) : group).ToArray() };

    public static Task StoreAsync(TestRig rig, QuotePricingDefinition definition, Guid? serviceId = null) => rig.InDb(async db =>
    {
        var id = serviceId ?? rig.ServiceId;
        var composition = await db.AmountCompositions.SingleOrDefaultAsync(row => row.PayableServiceId == id);
        if (composition is null)
        {
            composition = new() { Id = Guid.NewGuid(), PayableServiceId = id };
            db.AmountCompositions.Add(composition);
        }
        composition.PricingDefinitionJson = JsonSerializer.Serialize(definition, JsonSerializerOptions.Web);
        await db.SaveChangesAsync();
    });
}
