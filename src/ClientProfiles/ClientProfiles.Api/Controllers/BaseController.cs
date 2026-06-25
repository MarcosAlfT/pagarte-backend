using ClientProfiles.Application.Constants;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Utilities.Responses;

namespace ClientProfiles.Api.Controllers
{
	public class BaseController : ControllerBase
	{
		protected string? GetUserId()
		{
			return User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
		}

		protected IActionResult? ValidateUserId()
		{
			var userId = GetUserId();
			if (string.IsNullOrEmpty(userId))
				return Unauthorized(ApiResponse.CreateFailure(Messages.Auth.InvalidToken));
			return null;
		}
	}
}
