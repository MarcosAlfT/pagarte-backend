namespace Identity.Client.Application.Policies;

public sealed record PasswordPolicy(
    int MinimumLength,
    int MaximumLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireNumber,
    bool RequireSymbol,
    bool RejectEmailAsPassword,
    bool RejectCommonPasswords);

public sealed record TokenPolicy(
    int AccessTokenMinutes,
    int RefreshTokenDays,
    bool RefreshTokenRotationEnabled);

public sealed record LockoutPolicy(
    int MaxFailedLoginAttempts,
    int LockoutMinutes,
    int FailedAttemptWindowMinutes);

public sealed record EmailConfirmationPolicy(
    int TokenExpirationHours,
    int MaxResendAttemptsPerHour,
    string ClientConfirmationUrl);

public sealed record PasswordResetPolicy(
    int TokenExpirationMinutes,
    int MaxResetRequestsPerHour,
    string ClientResetUrl);

public sealed record PasskeyPolicy(
    int MaxPasskeysPerUser,
    bool RequirePasswordBeforeAddingPasskey,
    int ChallengeExpirationMinutes);
