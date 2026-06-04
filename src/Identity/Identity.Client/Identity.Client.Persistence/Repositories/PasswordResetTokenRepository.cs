using Identity.Client.Application.Abstractions;
using Identity.Client.Domain.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Identity.Client.Persistence.Repositories;

public sealed class PasswordResetTokenRepository(IdentityClientDbContext context) : IPasswordResetTokenRepository
{
    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken)
    {
        await context.PasswordResetTokens.AddAsync(token, cancellationToken);
    }

    public Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return context.PasswordResetTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }
}
