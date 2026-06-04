using Microsoft.EntityFrameworkCore;
using ClientProfiles.Api.Domain.Entities;
using ClientProfiles.Api.Interfaces;

namespace ClientProfiles.Api.Infrastructure.Repository
{
	public class ClientRepository(ClientProfilesDbContext context) : IClientRepository
	{
		private readonly ClientProfilesDbContext _context = context;

		public async Task<Client?> GetByClientIdAsync(Guid id)
		{
			return await _context.Clients
				.Include(c => c.Person)
				.Include(c => c.Organization)
				.Include(c => c.Addresses)
				.Include(c => c.Phones)
				.FirstOrDefaultAsync(c => c.Id == id);
		}
		public async Task<IEnumerable<Client>> GetAllAsync()
		{
			return await _context.Clients
				.Include(c => c.Person)
				.Include(c => c.Organization)
				.Include(c => c.Addresses)
				.Include(c => c.Phones)
				.ToListAsync();
		}
		public async Task<Client> CreateAsync(Client client)
		{
			_context.Clients.Add(client);
			await _context.SaveChangesAsync();
			return client;
		}
		public async Task UpdateAsync(Client client)
		{
			_context.Clients.Update(client);
			await _context.SaveChangesAsync();
		}
		public async Task DeleteAsync(Guid id)
		{
			var client = await _context.Clients.FindAsync(id);
			if (client != null)
			{
				client.Delete();
				await _context.SaveChangesAsync();
			}
		}

		public async Task<Client?> GetByUserIdAsync(string userId)
		{
			return await _context.Clients
			.Include(c => c.Person)
			.Include(c => c.Organization)
			.Include(c => c.Addresses)
			.Include(c => c.Phones)
			.FirstOrDefaultAsync(c => c.UserId == userId);
		}
	}
}
