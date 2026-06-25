using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Application.Abstractions
{
	public interface IClientRepository
	{
		Task<Client?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);
		Task<Client?> GetByClientIdAsync(Guid id, CancellationToken cancellationToken);
		Task<IEnumerable<Client>> GetAllAsync(CancellationToken cancellationToken);
		Task AddAsync(Client client, CancellationToken cancellationToken);
	}
}
