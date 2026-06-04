namespace Identity.Client.Application.Abstractions;

public interface INotificationPublisher
{
    Task PublishEmailConfirmationAsync(string email, string confirmationUrl, CancellationToken cancellationToken);
    Task PublishPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken);
}
