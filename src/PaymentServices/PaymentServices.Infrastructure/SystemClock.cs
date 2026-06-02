using PaymentServices.Application.Abstractions;

namespace PaymentServices.Infrastructure;

public sealed class SystemClock : IClock
{
	public DateTime UtcNow => DateTime.UtcNow;
}
