using Identity.Client.Domain.Common;
using Identity.Client.Domain.Passkeys;

namespace Identity.Client.Domain.Users;

public sealed class User : AuditableEntity
{
    private readonly List<UserPasskey> _passkeys = [];

    private User()
    {
    }

    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; }
    public DateTime? EmailConfirmedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LastFailedLoginAt { get; private set; }
    public DateTime? LockoutEndAt { get; private set; }
    public string SecurityStamp { get; private set; } = string.Empty;
    public string ConcurrencyStamp { get; private set; } = string.Empty;
    public IReadOnlyCollection<UserPasskey> Passkeys => _passkeys.AsReadOnly();

    public static User Create(string email, string normalizedEmail, string passwordHash, DateTime now, string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail.Trim(),
            PasswordHash = passwordHash,
            Status = UserStatus.PendingEmailConfirmation,
            EmailConfirmedAt = null,
            FailedLoginAttempts = 0,
            LastFailedLoginAt = null,
            LockoutEndAt = null,
            SecurityStamp = NewStamp(),
            ConcurrencyStamp = NewStamp(),
            DeletedAt = null,
            DeletedBy = null
        };

        user.CreateAudit(now, actor);
        return user;
    }

    public bool CanLogin(DateTime now)
    {
        if (DeletedAt is not null || Status is UserStatus.Deleted or UserStatus.Suspended or UserStatus.PendingEmailConfirmation)
        {
            return false;
        }

        return Status != UserStatus.Locked || LockoutEndAt <= now;
    }

    public void ConfirmEmail(DateTime now, string actor)
    {
        if (DeletedAt is not null || Status == UserStatus.Deleted)
        {
            throw new InvalidOperationException("Deleted users cannot confirm email.");
        }

        if (EmailConfirmedAt is not null)
        {
            return;
        }

        EmailConfirmedAt = now;
        Status = UserStatus.Active;
        Touch(now, actor);
    }

    public void RecordSuccessfulLogin(DateTime now, string actor)
    {
        FailedLoginAttempts = 0;
        LastFailedLoginAt = null;
        LockoutEndAt = null;
        Status = UserStatus.Active;
        LastLoginAt = now;
        Touch(now, actor);
    }

    public void RecordFailedLogin(DateTime now, int maxFailedAttempts, TimeSpan lockoutDuration, string actor)
    {
        FailedLoginAttempts++;
        LastFailedLoginAt = now;

        if (FailedLoginAttempts >= maxFailedAttempts)
        {
            Status = UserStatus.Locked;
            LockoutEndAt = now.Add(lockoutDuration);
        }

        Touch(now, actor);
    }

    public void UnlockIfLockoutExpired(DateTime now, string actor)
    {
        if (Status != UserStatus.Locked || LockoutEndAt > now)
        {
            return;
        }

        Status = UserStatus.Active;
        FailedLoginAttempts = 0;
        LockoutEndAt = null;
        Touch(now, actor);
    }

    public void ChangePassword(string passwordHash, DateTime now, string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        RotateSecurityStamp();
        Touch(now, actor);
    }

    public void Suspend(DateTime now, string actor)
    {
        Status = UserStatus.Suspended;
        RotateSecurityStamp();
        Touch(now, actor);
    }

    public override void SoftDelete(DateTime now, string actor)
    {
        Status = UserStatus.Deleted;
        RotateSecurityStamp();
        base.SoftDelete(now, actor);
    }

    public UserPasskey AddPasskey(
        string credentialId,
        string publicKey,
        string deviceName,
        uint signCount,
        DateTime now,
        string actor)
    {
        var passkey = UserPasskey.Create(Id, credentialId, publicKey, deviceName, signCount, now, actor);
        _passkeys.Add(passkey);
        Touch(now, actor);
        return passkey;
    }

    public void RemovePasskey(Guid passkeyId, DateTime now, string actor)
    {
        var passkey = _passkeys.FirstOrDefault(p => p.Id == passkeyId);
        passkey?.SoftDelete(now, actor);
        Touch(now, actor);
    }

    private void Touch(DateTime now, string actor)
    {
        ConcurrencyStamp = NewStamp();
        UpdateAudit(now, actor);
    }

    private void RotateSecurityStamp()
    {
        SecurityStamp = NewStamp();
    }

    private static string NewStamp()
    {
        return Guid.NewGuid().ToString("N");
    }
}
