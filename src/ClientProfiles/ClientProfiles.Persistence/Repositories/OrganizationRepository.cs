using Microsoft.EntityFrameworkCore;
using ClientProfiles.Application.Abstractions;
using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Persistence.Repositories
{
	public class OrganizationRepository(ClientProfilesDbContext context) : IOrganizationRepository
	{
		private readonly ClientProfilesDbContext _context = context;
		public async Task<Organization?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken)
		{
			return await _context.Organizations.FirstOrDefaultAsync(o => o.ClientId == clientId, cancellationToken);
		}
		public async Task AddAsync(Organization organization, CancellationToken cancellationToken)
		{
			await _context.Organizations.AddAsync(organization, cancellationToken);
		}
	}
}
