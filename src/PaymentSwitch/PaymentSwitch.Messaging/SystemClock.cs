namespace PaymentSwitch.Messaging
{
	public sealed class SystemClock : IClock
	{
		public DateTime UtcNow => DateTime.UtcNow;
	}
}
