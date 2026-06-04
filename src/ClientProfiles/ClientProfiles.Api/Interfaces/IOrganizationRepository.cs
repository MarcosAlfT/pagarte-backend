using ClientProfiles.Api.Domain.Entities;

namespace ClientProfiles.Api.Interfaces
{
	public interface IOrganizationRepository
	{
		Task<Organization?> GetByClientIdAsync(Guid clientId);
		Task<Organization> CreateAsync(Organization organization);
		Task UpdateAsync(Organization organization);
	}
}
