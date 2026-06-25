namespace PaymentSwitch.Messaging
{
	public interface IClock
	{
		DateTime UtcNow { get; }
	}
}
