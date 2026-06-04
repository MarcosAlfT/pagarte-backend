namespace PayableServices.Domain.Entities;

public sealed class AmountComponent
{
	public Guid Id { get; set; }
	public Guid AmountCompositionId { get; set; }
	public string ComponentType { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string Currency { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public string AppliesTo { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public bool IsVisibleToClient { get; set; }
	public int DisplayOrder { get; set; }
}
