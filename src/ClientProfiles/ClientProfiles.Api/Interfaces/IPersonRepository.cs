using ClientProfiles.Api.Domain.Entities;

namespace ClientProfiles.Api.Interfaces
{
	public interface IPersonRepository
	{
		Task<Person?> GetByClientIdAsync(Guid clientId);
		Task<Person> CreateAsync(Person person);
		Task UpdateAsync(Person person);
	}
}
