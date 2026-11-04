namespace PaymentSwitch.Processor.Domain.Entities;

public sealed class PaymentOperatorRoute
{
	public string IntegrationId { get; set; } = "";
	public string CredentialReference { get; set; } = "";
	public string Provider { get; set; } = "";
	public string Account { get; set; } = "";
	public string Environment { get; set; } = "";
	public string Version { get; set; } = "";
}
