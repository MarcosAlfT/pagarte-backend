using Microsoft.AspNetCore.Mvc;
using Pagarte.Security;
using Utilities.Responses;

namespace Payments.Api.Controllers
{
    public class BaseController : ControllerBase
    {
        protected string? GetClientId()
        {
            return User.FindFirst(KeycloakSecurity.ClientIdClaim)?.Value;
        }

        protected IActionResult? ValidateClientId()
        {
            var clientId = GetClientId();
            if (string.IsNullOrEmpty(clientId))
                return Unauthorized(ApiResponse.CreateFailure("Invalid or missing token."));
            return null;
        }
    }
}
