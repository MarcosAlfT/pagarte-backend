using Utilities.Responses;
using ClientProfiles.Api.DTOs.Responses;

namespace ClientProfiles.Api.Interfaces
{
	public interface IAddressService
	{
		Task<ApiResponse<IEnumerable<AddressResponse>>> GetByClientIdAsync(Guid clientId);
		Task<ApiResponse<AddressResponse>> CreateAsync(Guid clientId, string street, string city, string state, string postalCode, string country, bool isPrimary);
		Task<ApiResponse> UpdateAsync(Guid clientId, Guid addressId, string street, string city, string state, string postalCode, string country, bool isPrimary);
		Task<ApiResponse> DeleteAsync(Guid clientId, Guid addressId);
	}
}