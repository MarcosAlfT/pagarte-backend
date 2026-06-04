using Identity.Client.Application.Abstractions;
using Identity.Client.Domain.Tokens;
using Identity.Client.Domain.Users;
using FluentResults;

namespace Identity.Client.Application.UseCases;

public sealed class RefreshTokenUseCase(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ITokenGenerator tokenGenerator,
    ITokenHasher tokenHasher,
    ITokenService tokenService,
    IPolicyProvider policyProvider,
    ICurrentActorProvider actorProvider,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<RefreshTokenResponse>> ExecuteAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var existingToken = await refreshTokens.GetByTokenHashAsync(tokenHasher.Hash(request.RefreshToken), cancellationToken);
        if (existingToken is null || !existingToken.IsActive(now))
        {
            return Result.Fail<RefreshTokenResponse>("Refresh token is invalid or expired.");
        }

        var user = await users.GetByIdAsync(existingToken.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active || user.DeletedAt is not null)
        {
            return Result.Fail<RefreshTokenResponse>("User cannot refresh tokens.");
        }

        var actor = actorProvider.GetActorId();
        var rawReplacement = tokenGenerator.GenerateUrlSafeToken();
        var tokenPolicy = policyProvider.GetTokenPolicy();
        var replacement = RefreshToken.Create(
            user.Id,
            tokenHasher.Hash(rawReplacement),
            now.AddDays(tokenPolicy.RefreshTokenDays),
            request.DeviceId,
            request.DeviceName,
            actorProvider.GetUserAgent(),
            actorProvider.GetIpAddress(),
            now,
            actor);

        await refreshTokens.AddAsync(replacement, cancellationToken);
        existingToken.Revoke(now, actor, actorProvider.GetIpAddress(), replacement.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(new RefreshTokenResponse(tokenService.CreatePrincipal(user), rawReplacement));
    }
}
