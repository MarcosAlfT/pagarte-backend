using Identity.Client.Application.Abstractions;
using FluentResults;

namespace Identity.Client.Application.UseCases;

public sealed class ConfirmEmailUseCase(
    IUserRepository users,
    IEmailConfirmationTokenRepository emailTokens,
    ITokenHasher tokenHasher,
    ICurrentActorProvider actorProvider,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> ExecuteAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var token = await emailTokens.GetByTokenHashAsync(tokenHasher.Hash(request.Token), cancellationToken);
        var now = clock.UtcNow;

        if (token is null || !token.IsActive(now))
        {
            return Result.Fail("Email confirmation token is invalid or expired.");
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Fail("User was not found.");
        }

        var actor = actorProvider.GetActorId();
        user.ConfirmEmail(now, actor);
        token.MarkAsUsed(now, actor);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok().WithSuccess("Email confirmed successfully. You can now log in.");
    }
}
