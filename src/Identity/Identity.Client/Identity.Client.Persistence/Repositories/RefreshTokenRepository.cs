using Identity.Client.Application.Abstractions;
using Identity.Client.Domain.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Identity.Client.Persistence.Repositories;

public sealed class RefreshTokenRepository(IdentityClientDbContext context) : IRefreshTokenRepository
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        await context.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return context.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }

    public async Task RevokeActiveTokensForUserAsync(Guid userId, DateTime now, string actor, string? ipAddress, CancellationToken cancellationToken)
    {
        var tokens = await context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(now, actor, ipAddress);
        }
    }
}
