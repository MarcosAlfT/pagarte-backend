using Identity.Client.Domain.Common;

namespace Identity.Client.Domain.Passkeys;

public sealed class UserPasskey : AuditableEntity
{
    private UserPasskey()
    {
    }

    public Guid UserId { get; private set; }
    public string CredentialId { get; private set; } = string.Empty;
    public string PublicKey { get; private set; } = string.Empty;
    public string DeviceName { get; private set; } = string.Empty;
    public uint SignCount { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    public static UserPasskey Create(
        Guid userId,
        string credentialId,
        string publicKey,
        string deviceName,
        uint signCount,
        DateTime now,
        string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);

        var passkey = new UserPasskey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CredentialId = credentialId,
            PublicKey = publicKey,
            DeviceName = deviceName,
            SignCount = signCount
        };

        passkey.CreateAudit(now, actor);
        return passkey;
    }

    public void MarkUsed(uint signCount, DateTime now, string actor)
    {
        SignCount = signCount;
        LastUsedAt = now;
        UpdateAudit(now, actor);
    }
}
