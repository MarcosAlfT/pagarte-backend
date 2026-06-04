using Identity.Client.Domain.Common;

namespace Identity.Client.Domain.Tokens;

public sealed class EmailConfirmationToken : AuditableEntity
{
    private EmailConfirmationToken()
    {
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedBy { get; private set; }

    public bool IsActive(DateTime now)
    {
        return UsedAt is null && RevokedAt is null && DeletedAt is null && ExpiresAt > now;
    }

    public static EmailConfirmationToken Create(Guid userId, string tokenHash, DateTime expiresAt, DateTime now, string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var token = new EmailConfirmationToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };

        token.CreateAudit(now, actor);
        return token;
    }

    public void MarkAsUsed(DateTime now, string actor)
    {
        UsedAt = now;
        UpdateAudit(now, actor);
    }

    public void Revoke(DateTime now, string actor)
    {
        RevokedAt = now;
        RevokedBy = actor;
        UpdateAudit(now, actor);
    }
}
