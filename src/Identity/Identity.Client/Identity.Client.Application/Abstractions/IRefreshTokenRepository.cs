using Identity.Client.Domain.Tokens;

namespace Identity.Client.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task RevokeActiveTokensForUserAsync(Guid userId, DateTime now, string actor, string? ipAddress, CancellationToken cancellationToken);
}
