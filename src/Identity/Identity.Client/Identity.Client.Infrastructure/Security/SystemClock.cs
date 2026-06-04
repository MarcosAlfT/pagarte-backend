using Identity.Client.Application.Abstractions;

namespace Identity.Client.Infrastructure.Security;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
