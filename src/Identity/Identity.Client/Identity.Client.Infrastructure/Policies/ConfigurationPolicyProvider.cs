using Identity.Client.Application.Abstractions;
using Identity.Client.Application.Policies;
using Microsoft.Extensions.Configuration;

namespace Identity.Client.Infrastructure.Policies;

public sealed class ConfigurationPolicyProvider(IConfiguration configuration) : IPolicyProvider
{
    public PasswordPolicy GetPasswordPolicy()
    {
        var section = configuration.GetSection("IdentityPolicies:PasswordPolicy");
        return new PasswordPolicy(
            section.GetValue("MinimumLength", 10),
            section.GetValue("MaximumLength", 128),
            section.GetValue("RequireUppercase", true),
            section.GetValue("RequireLowercase", true),
            section.GetValue("RequireNumber", true),
            section.GetValue("RequireSymbol", true),
            section.GetValue("RejectEmailAsPassword", true),
            section.GetValue("RejectCommonPasswords", true));
    }

    public TokenPolicy GetTokenPolicy()
    {
        var section = configuration.GetSection("IdentityPolicies:TokenPolicy");
        return new TokenPolicy(
            section.GetValue("AccessTokenMinutes", 15),
            section.GetValue("RefreshTokenDays", 14),
            section.GetValue("RefreshTokenRotationEnabled", true));
    }

    public LockoutPolicy GetLockoutPolicy()
    {
        var section = configuration.GetSection("IdentityPolicies:LockoutPolicy");
        return new LockoutPolicy(
            section.GetValue("MaxFailedLoginAttempts", 5),
            section.GetValue("LockoutMinutes", 15),
            section.GetValue("FailedAttemptWindowMinutes", 30));
    }

    public EmailConfirmationPolicy GetEmailConfirmationPolicy()
    {
        var section = configuration.GetSection("IdentityPolicies:EmailConfirmationPolicy");
        return new EmailConfirmationPolicy(
            section.GetValue("TokenExpirationHours", 24),
            section.GetValue("MaxResendAttemptsPerHour", 3),
            section.GetValue("ClientConfirmationUrl", "https://localhost:3000/confirm-email"));
    }

    public PasswordResetPolicy GetPasswordResetPolicy()
    {
        var section = configuration.GetSection("IdentityPolicies:PasswordResetPolicy");
        return new PasswordResetPolicy(
            section.GetValue("TokenExpirationMinutes", 30),
            section.GetValue("MaxResetRequestsPerHour", 3),
            section.GetValue("ClientResetUrl", "https://localhost:3000/reset-password"));
    }

    public PasskeyPolicy GetPasskeyPolicy()
    {
        var section = configuration.GetSection("IdentityPolicies:PasskeyPolicy");
        return new PasskeyPolicy(
            section.GetValue("MaxPasskeysPerUser", 10),
            section.GetValue("RequirePasswordBeforeAddingPasskey", true),
            section.GetValue("ChallengeExpirationMinutes", 5));
    }
}
