using Identity.Client.Domain.Tokens;

namespace Identity.Client.Application.Abstractions;

public interface IEmailConfirmationTokenRepository
{
    Task AddAsync(EmailConfirmationToken token, CancellationToken cancellationToken);
    Task<EmailConfirmationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
}
