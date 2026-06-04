using System.Security.Claims;
using Identity.Client.Domain.Users;

namespace Identity.Client.Application.Abstractions;

public interface ITokenService
{
    ClaimsPrincipal CreatePrincipal(User user);
}
