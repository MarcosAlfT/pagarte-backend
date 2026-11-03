using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Application.Abstractions
{
	public interface IClientRepository
	{
		Task<Client?> GetByExternalIdentityAsync(string issuer, string subject, CancellationToken cancellationToken);
		Task<Guid?> GetClientIdByExternalIdentityAsync(string issuer, string subject, CancellationToken cancellationToken);
		Task<Client?> GetByClientIdAsync(Guid id, CancellationToken cancellationToken);
		Task<IEnumerable<Client>> GetAllAsync(CancellationToken cancellationToken);
		Task AddAsync(Client client, CancellationToken cancellationToken);
	}
}
