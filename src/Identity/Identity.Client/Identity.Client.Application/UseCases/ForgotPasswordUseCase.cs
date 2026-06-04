using Identity.Client.Application.Abstractions;
using Identity.Client.Application.Common;
using Identity.Client.Domain.Tokens;
using FluentResults;

namespace Identity.Client.Application.UseCases;

public sealed class ForgotPasswordUseCase(
    IUserRepository users,
    IPasswordResetTokenRepository resetTokens,
    ITokenGenerator tokenGenerator,
    ITokenHasher tokenHasher,
    IPolicyProvider policyProvider,
    INotificationPublisher notifications,
    ICurrentActorProvider actorProvider,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> ExecuteAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByNormalizedEmailAsync(IdentityNormalization.NormalizeEmail(request.Email), cancellationToken);
        if (user is null)
        {
            return Result.Ok().WithSuccess("If the email exists, a password reset link will be sent.");
        }

        var now = clock.UtcNow;
        var actor = actorProvider.GetActorId();
        var rawToken = tokenGenerator.GenerateUrlSafeToken();
        var policy = policyProvider.GetPasswordResetPolicy();
        var resetToken = PasswordResetToken.Create(
            user.Id,
            tokenHasher.Hash(rawToken),
            now.AddMinutes(policy.TokenExpirationMinutes),
            now,
            actor);

        await resetTokens.AddAsync(resetToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{policy.ClientResetUrl}?token={Uri.EscapeDataString(rawToken)}";
        await notifications.PublishPasswordResetAsync(user.Email, resetUrl, cancellationToken);

        return Result.Ok().WithSuccess("If the email exists, a password reset link will be sent.");
    }
}
