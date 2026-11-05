namespace PayableServices.Application.Models;

public sealed record QuotePricingDefinition(
    string Root, IReadOnlyList<QuotePricingGroup> Groups, IReadOnlyList<QuotePricingLeaf> Leaves);

public sealed record QuotePricingGroup(string Id, string Label, IReadOnlyList<string> Children);

public sealed record QuotePricingLeaf(string Id, string Type, string Label, string Currency,
    string Strategy, int Version, decimal? Amount = null, decimal? Rate = null, IReadOnlyList<string>? Bases = null);

public sealed record QuoteBreakdownNode(string Kind, string Label, long? MinorUnits,
    Guid? ItemId, string? ReferenceValue, IReadOnlyList<QuoteBreakdownNode> Children);

internal sealed record QuotePricingEvidence(Guid ItemId, QuotePricingLeaf Rule, long? BaseMinorUnits);

internal sealed record QuotePricingSnapshot(int Version, QuoteBreakdownNode Breakdown,
    IReadOnlyList<QuotePricingEvidence> Evidence);
