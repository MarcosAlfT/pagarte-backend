using Identity.Client.Application.Policies;

namespace Identity.Client.Application.Abstractions;

public interface IPolicyProvider
{
    PasswordPolicy GetPasswordPolicy();
    TokenPolicy GetTokenPolicy();
    LockoutPolicy GetLockoutPolicy();
    EmailConfirmationPolicy GetEmailConfirmationPolicy();
    PasswordResetPolicy GetPasswordResetPolicy();
    PasskeyPolicy GetPasskeyPolicy();
}
