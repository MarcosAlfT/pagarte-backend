namespace Identity.Client.Application.Abstractions;

public interface ICurrentActorProvider
{
    string GetActorId();
    string? GetIpAddress();
    string? GetUserAgent();
}
