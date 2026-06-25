using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Application.Abstractions
{
	public interface IPhoneRepository
	{
		Task<IEnumerable<Phone>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken);
		Task AddAsync(Phone phone, CancellationToken cancellationToken);
		Task SetPrimaryPhoneAsync(Guid clientId, Guid phoneId, CancellationToken cancellationToken);
	}
}
