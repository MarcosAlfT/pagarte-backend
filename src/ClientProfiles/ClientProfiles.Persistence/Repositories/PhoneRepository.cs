using Microsoft.EntityFrameworkCore;
using ClientProfiles.Application.Abstractions;
using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Persistence.Repositories
{
	public class PhoneRepository(ClientProfilesDbContext context): IPhoneRepository
	{
		private readonly ClientProfilesDbContext _context = context;
		public async Task<IEnumerable<Phone>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken)
		{
			return await _context.Phones.Where(p => p.ClientId == clientId).ToListAsync(cancellationToken);
		}
		public async Task AddAsync(Phone phone, CancellationToken cancellationToken)
		{
			await _context.Phones.AddAsync(phone, cancellationToken);
		}
		public async Task SetPrimaryPhoneAsync(Guid clientId, Guid phoneId, CancellationToken cancellationToken)
		{
			var now = DateTime.UtcNow;
			await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

			await _context.Phones
				.Where(phone => phone.ClientId == clientId && phone.IsPrimary)
				.ExecuteUpdateAsync(
					setters => setters
						.SetProperty(phone => phone.IsPrimary, false)
						.SetProperty(phone => phone.UpdatedAt, now),
					cancellationToken);

			await _context.Phones
				.Where(phone => phone.ClientId == clientId && phone.Id == phoneId)
				.ExecuteUpdateAsync(
					setters => setters
						.SetProperty(phone => phone.IsPrimary, true)
						.SetProperty(phone => phone.UpdatedAt, now),
					cancellationToken);

			await transaction.CommitAsync(cancellationToken);
		}
	}
}
