namespace PaymentSwitch.Processor.Domain.Entities;

public sealed class PaymentReferenceValue
{
	public Guid Id { get; set; }
	public Guid PaymentId { get; set; }
	public string Key { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
	public Payment Payment { get; set; } = null!;
}
