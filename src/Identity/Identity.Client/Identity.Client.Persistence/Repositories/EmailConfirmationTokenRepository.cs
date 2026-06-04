using Identity.Client.Application.Abstractions;
using Identity.Client.Domain.Tokens;
using Microsoft.EntityFrameworkCore;

namespace Identity.Client.Persistence.Repositories;

public sealed class EmailConfirmationTokenRepository(IdentityClientDbContext context) : IEmailConfirmationTokenRepository
{
    public async Task AddAsync(EmailConfirmationToken token, CancellationToken cancellationToken)
    {
        await context.EmailConfirmationTokens.AddAsync(token, cancellationToken);
    }

    public Task<EmailConfirmationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return context.EmailConfirmationTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
    }
}
