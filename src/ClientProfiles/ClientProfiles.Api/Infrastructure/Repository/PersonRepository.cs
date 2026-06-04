using ClientProfiles.Api.Domain.Entities;
using ClientProfiles.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ClientProfiles.Api.Infrastructure.Repository
{
	public class PersonRepository(ClientProfilesDbContext context) : IPersonRepository
	{
		private readonly ClientProfilesDbContext _context = context;
		public async Task<Person?> GetByClientIdAsync(Guid clientId)
		{
			return await _context.Persons.FirstOrDefaultAsync(p => p.ClientId == clientId);
		}
		public async Task<Person> CreateAsync(Person person)
		{
			_context.Persons.Add(person);
			await _context.SaveChangesAsync();
			return person;
		}
		public async Task UpdateAsync(Person person)
		{
			_context.Persons.Update(person);
			await _context.SaveChangesAsync();
		}
	}
}
