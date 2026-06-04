using Identity.Client.Application.Abstractions;
using Identity.Client.Application.Common;
using Identity.Client.Domain.Users;
using FluentResults;

namespace Identity.Client.Application.UseCases;

public sealed class LoginWithPasswordUseCase(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IPolicyProvider policyProvider,
    ICurrentActorProvider actorProvider,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<LoginWithPasswordResponse>> ExecuteAsync(LoginWithPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(request.Email, cancellationToken);
        if (user is null)
        {
            return Result.Fail<LoginWithPasswordResponse>("Invalid email or password.");
        }

        var now = clock.UtcNow;
        var actor = actorProvider.GetActorId();
        user.UnlockIfLockoutExpired(now, actor);

        if (!ValidatePassword(user, request.Password))
        {
            await TrackFailedLoginAsync(user, now, actor, cancellationToken);
            return Result.Fail<LoginWithPasswordResponse>("Invalid email or password.");
        }

        if (!ValidateAccountStatus(user, now, out var statusError))
        {
            return Result.Fail<LoginWithPasswordResponse>(statusError);
        }

        await TrackSuccessfulLoginAsync(user, now, actor, cancellationToken);
        var tokens = IssueTokens(user);
        await AuditLoginAsync(cancellationToken);

        return tokens;
    }

    private Task<User?> FindUserAsync(string email, CancellationToken cancellationToken)
    {
        return users.GetByNormalizedEmailAsync(IdentityNormalization.NormalizeEmail(email), cancellationToken);
    }

    private bool ValidatePassword(User user, string password)
    {
        return passwordHasher.Verify(password, user.PasswordHash);
    }

    private bool ValidateAccountStatus(User user, DateTime now, out string error)
    {
        if (user.Status == UserStatus.PendingEmailConfirmation)
        {
            error = "Email is not confirmed.";
            return false;
        }

        if (user.Status == UserStatus.Suspended)
        {
            error = "User is suspended.";
            return false;
        }

        if (user.Status == UserStatus.Deleted || user.DeletedAt is not null)
        {
            error = "User is deleted.";
            return false;
        }

        if (!user.CanLogin(now))
        {
            error = "User is locked.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private async Task TrackFailedLoginAsync(User user, DateTime now, string actor, CancellationToken cancellationToken)
    {
        var policy = policyProvider.GetLockoutPolicy();
        user.RecordFailedLogin(now, policy.MaxFailedLoginAttempts, TimeSpan.FromMinutes(policy.LockoutMinutes), actor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task TrackSuccessfulLoginAsync(User user, DateTime now, string actor, CancellationToken cancellationToken)
    {
        user.RecordSuccessfulLogin(now, actor);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Result<LoginWithPasswordResponse> IssueTokens(User user)
    {
        return Result.Ok(new LoginWithPasswordResponse(tokenService.CreatePrincipal(user)));
    }

    private static Task AuditLoginAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
