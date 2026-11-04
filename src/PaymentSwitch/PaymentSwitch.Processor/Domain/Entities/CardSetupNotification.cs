namespace PaymentSwitch.Processor.Domain.Entities;

public sealed class CardSetupNotification
{
	public Guid Id { get; set; }
	public string IntegrationId { get; set; } = "";
	public string EventId { get; set; } = "";
	public Guid SessionId { get; set; }
	public DateTime ReceivedAt { get; set; }
}
