using Identity.Client.Application.Abstractions;
using Identity.Client.Application.PasswordValidation;
using FluentResults;

namespace Identity.Client.Application.UseCases;

public sealed class ChangePasswordUseCase(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IPolicyProvider policyProvider,
    ICurrentActorProvider actorProvider,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private readonly PasswordValidator _passwordValidator = new PasswordValidator.Builder().UseDefaultRules().Build();

    public async Task<Result> ExecuteAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
        {
            return Result.Fail("User was not found.");
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Fail("Current password is invalid.");
        }

        var passwordValidation = _passwordValidator.Validate(request.NewPassword, user.Email, policyProvider.GetPasswordPolicy());
        if (!passwordValidation.IsValid)
        {
            return Result.Fail(passwordValidation.Errors.Select(error => new Error(error)));
        }

        var now = clock.UtcNow;
        var actor = actorProvider.GetActorId();
        user.ChangePassword(passwordHasher.Hash(request.NewPassword), now, actor);
        await refreshTokens.RevokeActiveTokensForUserAsync(user.Id, now, actor, actorProvider.GetIpAddress(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok().WithSuccess("Password changed successfully.");
    }
}
