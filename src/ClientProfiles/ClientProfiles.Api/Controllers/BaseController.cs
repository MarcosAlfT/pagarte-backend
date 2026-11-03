using ClientProfiles.Application.Constants;
using Microsoft.AspNetCore.Mvc;
using Utilities.Responses;

namespace ClientProfiles.Api.Controllers
{
	public class BaseController : ControllerBase
	{
		protected string? GetSubject()
		{
			return User.FindFirst("sub")?.Value;
		}

		protected string? GetIssuer() => User.FindFirst("iss")?.Value;

		protected IActionResult? ValidateExternalIdentity()
		{
			if (string.IsNullOrWhiteSpace(GetIssuer()) || string.IsNullOrWhiteSpace(GetSubject()))
				return Unauthorized(ApiResponse.CreateFailure(Messages.Auth.InvalidToken));
			return null;
		}
	}
}
