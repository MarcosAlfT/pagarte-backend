using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Identity.Client.Application.UseCases;

public sealed record RegisterUserRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public sealed record ConfirmEmailRequest([property: Required] string Token);

public sealed record LoginWithPasswordRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public sealed record LoginWithPasswordResponse(ClaimsPrincipal Principal);

public sealed record RefreshTokenRequest(
    [property: Required] string RefreshToken,
    string? DeviceId,
    string? DeviceName);

public sealed record RefreshTokenResponse(ClaimsPrincipal Principal, string RefreshToken);

public sealed record LogoutRequest([property: Required] string RefreshToken);

public sealed record ForgotPasswordRequest([property: Required, EmailAddress] string Email);

public sealed record ResetPasswordRequest(
    [property: Required] string Token,
    [property: Required] string NewPassword);

public sealed record ChangePasswordRequest(
    [property: Required] Guid UserId,
    [property: Required] string CurrentPassword,
    [property: Required] string NewPassword);
