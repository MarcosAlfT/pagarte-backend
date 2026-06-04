using Identity.Client.Application.Abstractions;
using Identity.Client.Application.PasswordValidation;
using FluentResults;

namespace Identity.Client.Application.UseCases;

public sealed class ResetPasswordUseCase(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ITokenHasher tokenHasher,
    IPolicyProvider policyProvider,
    ICurrentActorProvider actorProvider,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private readonly PasswordValidator _passwordValidator = new PasswordValidator.Builder().UseDefaultRules().Build();

    public async Task<Result> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var resetToken = await resetTokens.GetByTokenHashAsync(tokenHasher.Hash(request.Token), cancellationToken);
        if (resetToken is null || !resetToken.IsActive(now))
        {
            return Result.Fail("Password reset token is invalid or expired.");
        }

        var user = await users.GetByIdAsync(resetToken.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Fail("User was not found.");
        }

        var passwordValidation = _passwordValidator.Validate(request.NewPassword, user.Email, policyProvider.GetPasswordPolicy());
        if (!passwordValidation.IsValid)
        {
            return Result.Fail(passwordValidation.Errors.Select(error => new Error(error)));
        }

        var actor = actorProvider.GetActorId();
        user.ChangePassword(passwordHasher.Hash(request.NewPassword), now, actor);
        resetToken.MarkAsUsed(now, actor);
        await refreshTokens.RevokeActiveTokensForUserAsync(user.Id, now, actor, actorProvider.GetIpAddress(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok().WithSuccess("Password reset successfully.");
    }
}
