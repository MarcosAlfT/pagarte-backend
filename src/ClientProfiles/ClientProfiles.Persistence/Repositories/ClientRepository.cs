using Microsoft.EntityFrameworkCore;
using ClientProfiles.Application.Abstractions;
using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Persistence.Repositories
{
	public class ClientRepository(ClientProfilesDbContext context) : IClientRepository
	{
		private readonly ClientProfilesDbContext _context = context;

		public async Task<Client?> GetByClientIdAsync(Guid id, CancellationToken cancellationToken)
		{
			return await _context.Clients
				.Include(c => c.Person)
				.Include(c => c.Organization)
				.Include(c => c.Addresses)
				.Include(c => c.Phones)
				.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
		}
		public async Task<IEnumerable<Client>> GetAllAsync(CancellationToken cancellationToken)
		{
			return await _context.Clients
				.Include(c => c.Person)
				.Include(c => c.Organization)
				.Include(c => c.Addresses)
				.Include(c => c.Phones)
				.ToListAsync(cancellationToken);
		}
		public async Task AddAsync(Client client, CancellationToken cancellationToken)
		{
			await _context.Clients.AddAsync(client, cancellationToken);
		}
		public async Task<Client?> GetByUserIdAsync(string userId, CancellationToken cancellationToken)
		{
			return await _context.Clients
			.Include(c => c.Person)
			.Include(c => c.Organization)
			.Include(c => c.Addresses)
			.Include(c => c.Phones)
			.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
		}
	}
}
