using ClientProfiles.Domain.Entities;

namespace ClientProfiles.Application.Abstractions
{
	public interface IAddressRepository
	{
		Task<IEnumerable<Address>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken);
		Task AddAsync(Address address, CancellationToken cancellationToken);
		Task SetPrimaryAddressAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken);
	}
}
