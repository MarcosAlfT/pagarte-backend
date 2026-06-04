using Identity.Client.Application.Abstractions;
using Identity.Client.Application.Common;
using Identity.Client.Application.PasswordValidation;
using Identity.Client.Domain.Tokens;
using Identity.Client.Domain.Users;
using FluentResults;

namespace Identity.Client.Application.UseCases;

public sealed class RegisterUserUseCase(
    IUserRepository users,
    IEmailConfirmationTokenRepository emailTokens,
    IPasswordHasher passwordHasher,
    ITokenGenerator tokenGenerator,
    ITokenHasher tokenHasher,
    IPolicyProvider policyProvider,
    INotificationPublisher notifications,
    ICurrentActorProvider actorProvider,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    private readonly PasswordValidator _passwordValidator = new PasswordValidator.Builder().UseDefaultRules().Build();

    public async Task<Result> ExecuteAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = IdentityNormalization.NormalizeEmail(request.Email);

        if (await users.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken))
        {
            return Result.Fail("Email already exists.");
        }

        var passwordValidation = _passwordValidator.Validate(request.Password, request.Email, policyProvider.GetPasswordPolicy());
        if (!passwordValidation.IsValid)
        {
            return Result.Fail(passwordValidation.Errors.Select(error => new Error(error)));
        }

        var now = clock.UtcNow;
        var actor = actorProvider.GetActorId();
        var user = User.Create(request.Email, normalizedEmail, passwordHasher.Hash(request.Password), now, actor);
        var rawToken = tokenGenerator.GenerateUrlSafeToken();
        var policy = policyProvider.GetEmailConfirmationPolicy();
        var token = EmailConfirmationToken.Create(
            user.Id,
            tokenHasher.Hash(rawToken),
            now.AddHours(policy.TokenExpirationHours),
            now,
            actor);

        await users.AddAsync(user, cancellationToken);
        await emailTokens.AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var confirmationUrl = $"{policy.ClientConfirmationUrl}?token={Uri.EscapeDataString(rawToken)}";
        await notifications.PublishEmailConfirmationAsync(user.Email, confirmationUrl, cancellationToken);

        return Result.Ok().WithSuccess("Registration success. Please check your email.");
    }
}
