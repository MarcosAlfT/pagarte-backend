namespace PaymentSwitch.Processor.Domain.Entities;

public sealed class PaymentDetail
{
	public Guid Id { get; set; }
	public Guid PaymentId { get; set; }
	public string ComponentType { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Source { get; set; } = string.Empty;
	public bool IsVisibleToClient { get; set; }
	public int DisplayOrder { get; set; }
	public long MinorUnits { get; set; }
	public string Currency { get; set; } = string.Empty;
	public Payment Payment { get; set; } = null!;
}
