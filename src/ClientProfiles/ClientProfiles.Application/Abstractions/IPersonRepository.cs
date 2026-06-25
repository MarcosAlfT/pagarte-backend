using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Application.Abstractions
{
	public interface IPersonRepository
	{
		Task<Person?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken);
		Task AddAsync(Person person, CancellationToken cancellationToken);
	}
}
