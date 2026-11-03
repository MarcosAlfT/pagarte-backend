using System.Security.Cryptography;
using System.Text;
using ClientProfiles.Application.UseCases.Clients;
using Microsoft.AspNetCore.Mvc;

namespace ClientProfiles.Api.Controllers;

public sealed record ResolveClientRequest(string Issuer, string Subject);
public sealed record ResolveClientResponse(Guid ClientId);

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/client-identity")]
public sealed class InternalClientIdentityController(
    ResolveClientIdUseCase resolveClientId,
    IConfiguration configuration,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("resolve")]
    public async Task<IActionResult> ResolveAsync(
        [FromBody] ResolveClientRequest request,
        CancellationToken cancellationToken)
    {
        if (!Request.IsHttps && !environment.IsDevelopment())
            return StatusCode(StatusCodes.Status403Forbidden);

        var configuredKey = configuration["ClientIdentityResolver:ApiKey"];
        var suppliedKey = Request.Headers["X-Pagarte-Resolver-Key"].ToString();
        if (string.IsNullOrWhiteSpace(configuredKey) || string.IsNullOrWhiteSpace(suppliedKey) ||
            !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey)),
                SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey))))
            return Unauthorized();

        if (request.Issuer != configuration["AuthSettings:Authority"]?.TrimEnd('/'))
            return BadRequest();

        var clientId = await resolveClientId.ExecuteAsync(
            request.Issuer, request.Subject, cancellationToken);
        return clientId is null ? NotFound() : Ok(new ResolveClientResponse(clientId.Value));
    }
}
