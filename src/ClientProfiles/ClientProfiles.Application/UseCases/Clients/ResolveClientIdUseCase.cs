using ClientProfiles.Application.Abstractions;

namespace ClientProfiles.Application.UseCases.Clients;

public sealed class ResolveClientIdUseCase(IClientRepository clients)
{
    public async Task<Guid?> ExecuteAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject))
            return null;
        return await clients.GetClientIdByExternalIdentityAsync(issuer, subject, cancellationToken);
    }
}
