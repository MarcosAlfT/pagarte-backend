using Identity.Client.Application.Abstractions;

namespace Identity.Client.Infrastructure.Notifications;

public sealed class ConsoleNotificationPublisher : INotificationPublisher
{
    public Task PublishEmailConfirmationAsync(string email, string confirmationUrl, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Email confirmation for {email}: {confirmationUrl}");
        return Task.CompletedTask;
    }

    public Task PublishPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Password reset for {email}: {resetUrl}");
        return Task.CompletedTask;
    }
}
