using ClientProfiles.Application.Abstractions;
using ClientProfiles.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientProfiles.Persistence.Repositories
{
	public class PersonRepository(ClientProfilesDbContext context) : IPersonRepository
	{
		private readonly ClientProfilesDbContext _context = context;
		public async Task<Person?> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken)
		{
			return await _context.Persons.FirstOrDefaultAsync(p => p.ClientId == clientId, cancellationToken);
		}
		public async Task AddAsync(Person person, CancellationToken cancellationToken)
		{
			await _context.Persons.AddAsync(person, cancellationToken);
		}
	}
}
