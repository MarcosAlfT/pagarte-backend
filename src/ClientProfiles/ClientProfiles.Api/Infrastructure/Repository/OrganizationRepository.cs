using Microsoft.EntityFrameworkCore;
using ClientProfiles.Api.Domain.Entities;
using ClientProfiles.Api.Interfaces;

namespace ClientProfiles.Api.Infrastructure.Repository
{
	public class OrganizationRepository(ClientProfilesDbContext context) : IOrganizationRepository
	{
		private readonly ClientProfilesDbContext _context = context;
		public async Task<Organization?> GetByClientIdAsync(Guid clientId)
		{
			return await _context.Organizations.FirstOrDefaultAsync(o => o.ClientId == clientId);
		}
		public async Task<Organization> CreateAsync(Organization organization)
		{
			_context.Organizations.Add(organization);
			await _context.SaveChangesAsync();
			return organization;
		}
		public async Task UpdateAsync(Organization organization)
		{
			_context.Organizations.Update(organization);
			await _context.SaveChangesAsync();
		}
	}
}