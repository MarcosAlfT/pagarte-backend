using PayableServices.Application.Abstractions;

namespace PayableServices.Infrastructure;

public sealed class SystemClock : IClock
{
	public DateTime UtcNow => DateTime.UtcNow;
}
