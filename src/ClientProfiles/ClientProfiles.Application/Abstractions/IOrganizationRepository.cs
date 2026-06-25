using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Application.Abstractions
{
	public interface IOrganizationRepository
	{
		Task<Organization?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken);
		Task AddAsync(Organization organization, CancellationToken cancellationToken);
	}
}
