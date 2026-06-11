using Identity.Client.Application.UseCases;
using Utilities.Responses;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Identity.Client.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    RegisterUserUseCase registerUser,
    ConfirmEmailUseCase confirmEmail,
    LoginWithPasswordUseCase loginWithPassword,
    RefreshTokenUseCase refreshToken,
    LogoutUseCase logout,
    ForgotPasswordUseCase forgotPassword,
    ResetPasswordUseCase resetPassword,
    ChangePasswordUseCase changePassword) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.CreateFailure("Input data error."));
        }

        var result = await registerUser.ExecuteAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("confirm-email")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ConfirmEmailAsync([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(ApiResponse.CreateFailure("Missing confirmation token."));
        }

        var result = await confirmEmail.ExecuteAsync(new ConfirmEmailRequest(token), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("~/connect/token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExchangeAsync(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest(ApiResponse.CreateFailure("Invalid OpenIddict request."));
        }

        return request.GrantType switch
        {
            OpenIddictConstants.GrantTypes.Password => await ExchangePasswordGrantAsync(request, cancellationToken),
            OpenIddictConstants.GrantTypes.RefreshToken => await ExchangeRefreshTokenGrantAsync(request, cancellationToken),
            _ => ForbidOpenIddict(OpenIddictConstants.Errors.UnsupportedGrantType, "The specified grant type is not supported.")
        };
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await logout.ExecuteAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await forgotPassword.ExecuteAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await resetPassword.ExecuteAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ChangePasswordAsync(ChangePasswordApiRequest request, CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(ApiResponse.CreateFailure("Authenticated user id is missing."));
        }

        var result = await changePassword.ExecuteAsync(
            new ChangePasswordRequest(userId, request.CurrentPassword, request.NewPassword),
            cancellationToken);

        return ToActionResult(result);
    }

    private async Task<IActionResult> ExchangePasswordGrantAsync(OpenIddictRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponse.CreateFailure("Username and password are required."));
        }

        var result = await loginWithPassword.ExecuteAsync(
            new LoginWithPasswordRequest(request.Username, request.Password, null, null),
            cancellationToken);

        if (result.IsFailed)
        {
            return ForbidOpenIddict(OpenIddictConstants.Errors.InvalidGrant, string.Join(" ", result.Errors.Select(error => error.Message)));
        }

        return SignIn(
            result.Value.Principal,
            CreateTokenResponseProperties(result.Value.RefreshToken),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> ExchangeRefreshTokenGrantAsync(OpenIddictRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return ForbidOpenIddict(OpenIddictConstants.Errors.InvalidGrant, "Refresh token is invalid.");
        }

        var result = await refreshToken.ExecuteAsync(
            new RefreshTokenRequest(request.RefreshToken, null, null),
            cancellationToken);

        if (result.IsFailed)
        {
            return ForbidOpenIddict(OpenIddictConstants.Errors.InvalidGrant, string.Join(" ", result.Errors.Select(error => error.Message)));
        }

        return SignIn(
            result.Value.Principal,
            CreateTokenResponseProperties(result.Value.RefreshToken),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static AuthenticationProperties CreateTokenResponseProperties(string refreshToken)
    {
        var properties = new AuthenticationProperties();
        properties.SetParameter(OpenIddictConstants.Parameters.RefreshToken, refreshToken);
        return properties;
    }

    private ForbidResult ForbidOpenIddict(string error, string description)
    {
        return Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }));
    }

    private ActionResult<ApiResponse> ToActionResult(FluentResults.Result result)
    {
        if (result.IsFailed)
        {
            return BadRequest(ApiResponse.CreateFailure(string.Join(" ", result.Errors.Select(error => error.Message))));
        }

        var message = result.Successes.FirstOrDefault()?.Message ?? "Operation succeeded.";
        return Ok(ApiResponse.CreateSuccess(message));
    }
}

public sealed record ChangePasswordApiRequest(string CurrentPassword, string NewPassword);
