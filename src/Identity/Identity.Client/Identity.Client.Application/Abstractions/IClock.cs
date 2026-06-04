namespace Identity.Client.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
