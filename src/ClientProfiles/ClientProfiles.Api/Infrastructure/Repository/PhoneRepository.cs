using Microsoft.EntityFrameworkCore;
using ClientProfiles.Api.Domain.Entities;
using ClientProfiles.Api.Interfaces;

namespace ClientProfiles.Api.Infrastructure.Repository
{
	public class PhoneRepository(ClientProfilesDbContext context): IPhoneRepository
	{
		private readonly ClientProfilesDbContext _context = context;
		public async Task<IEnumerable<Phone>> GetByClientIdAsync(Guid clientId)
		{
			return await _context.Phones.Where(p => p.ClientId == clientId).ToListAsync();
		}
		public async Task<Phone> CreateAsync(Phone phone)
		{
			_context.Phones.Add(phone);
			await _context.SaveChangesAsync();
			return phone;
		}
		public async Task UpdateAsync(Phone phone)
		{
			_context.Phones.Update(phone);
			await _context.SaveChangesAsync();
		}
		 public async Task DeleteAsync(Guid clientId, Guid phoneId)
		{
			var phone = await _context.Phones.FirstOrDefaultAsync(p => p.ClientId == clientId && p.Id == phoneId);
			if (phone != null)
			{
				phone.DeletePhone();
				await _context.SaveChangesAsync();
			}
		}
	}
}
