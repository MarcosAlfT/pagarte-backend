using Identity.Client.Domain.Common;

namespace Identity.Client.Domain.Tokens;

public sealed class RefreshToken : AuditableEntity
{
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedBy { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? DeviceId { get; private set; }
    public string? DeviceName { get; private set; }
    public string? UserAgent { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? RevokedByIp { get; private set; }

    public bool IsActive(DateTime now)
    {
        return RevokedAt is null && DeletedAt is null && ExpiresAt > now;
    }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        string? deviceId,
        string? deviceName,
        string? userAgent,
        string? createdByIp,
        DateTime now,
        string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            DeviceId = deviceId,
            DeviceName = deviceName,
            UserAgent = userAgent,
            CreatedByIp = createdByIp
        };

        token.CreateAudit(now, actor);
        return token;
    }

    public void Revoke(DateTime now, string actor, string? revokedByIp, Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedBy = actor;
        RevokedByIp = revokedByIp;
        ReplacedByTokenId = replacedByTokenId;
        UpdateAudit(now, actor);
    }
}
