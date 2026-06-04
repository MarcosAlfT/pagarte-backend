using ClientProfiles.Api.Domain.Entities;

namespace ClientProfiles.Api.Interfaces
{
	public interface IAddressRepository
	{
		Task<IEnumerable<Address>> GetByClientIdAsync(Guid clientId);
		Task<Address> CreateAsync(Address address);
		Task UpdateAsync(Address address);
		Task DeleteAsync(Guid clientId, Guid addressId);
	}
}
