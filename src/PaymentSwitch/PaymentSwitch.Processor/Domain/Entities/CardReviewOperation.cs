namespace PaymentSwitch.Processor.Domain.Entities;

public sealed class CardReviewOperation
{
	public Guid Id { get; set; }
	public Guid TargetId { get; set; }
	public string Kind { get; set; } = "";
	public string Action { get; set; } = "";
	public string Actor { get; set; } = "";
	public string RequestKey { get; set; } = "";
	public string Fingerprint { get; set; } = "";
	public string ReasonCode { get; set; } = "";
	public string BeforeState { get; set; } = "";
	public string AfterState { get; set; } = "";
	public string Outcome { get; set; } = "Pending";
	public DateTime CreatedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
}
