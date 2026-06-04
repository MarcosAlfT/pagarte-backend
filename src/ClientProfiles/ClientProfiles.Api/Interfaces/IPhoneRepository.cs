using ClientProfiles.Api.Domain.Entities;

namespace ClientProfiles.Api.Interfaces
{
	public interface IPhoneRepository
	{
		Task<IEnumerable<Phone>> GetByClientIdAsync(Guid clientId);
		Task<Phone> CreateAsync(Phone phone);
		Task UpdateAsync(Phone phone);
		Task DeleteAsync(Guid clientId, Guid phoneId);
	}
}
