using Microsoft.EntityFrameworkCore;
using ClientProfiles.Application.Abstractions;
using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Persistence.Repositories
{
	public class AddressRepository(ClientProfilesDbContext context) : IAddressRepository
	{
		private readonly ClientProfilesDbContext _context = context;
		public async Task<IEnumerable<Address>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken)
		{
			return await _context.Addresses.Where(a => a.ClientId == clientId).ToListAsync(cancellationToken);
		}
		public async Task AddAsync(Address address, CancellationToken cancellationToken)
		{
			await _context.Addresses.AddAsync(address, cancellationToken);
		}
		public async Task SetPrimaryAddressAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken)
		{
			var now = DateTime.UtcNow;
			await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

			await _context.Addresses
				.Where(address => address.ClientId == clientId && address.IsPrimary)
				.ExecuteUpdateAsync(
					setters => setters
						.SetProperty(address => address.IsPrimary, false)
						.SetProperty(address => address.LastUpdatedAt, now),
					cancellationToken);

			await _context.Addresses
				.Where(address => address.ClientId == clientId && address.Id == addressId)
				.ExecuteUpdateAsync(
					setters => setters
						.SetProperty(address => address.IsPrimary, true)
						.SetProperty(address => address.LastUpdatedAt, now),
					cancellationToken);

			await transaction.CommitAsync(cancellationToken);
		}
	}
}
