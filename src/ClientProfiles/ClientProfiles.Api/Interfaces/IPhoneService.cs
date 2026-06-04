using ClientProfiles.Api.Domain;
using Utilities.Responses;
using ClientProfiles.Api.DTOs.Responses;

namespace ClientProfiles.Api.Interfaces
{
	public interface IPhoneService
	{
		Task<ApiResponse<IEnumerable<PhoneResponse>>> GetByClientIdAsync(Guid clientId);
		Task<ApiResponse<PhoneResponse>> CreateAsync(Guid clientId, string number, PhoneType type, bool isPrimary);
		Task<ApiResponse> UpdateAsync(Guid clientId, Guid phoneId, string number, PhoneType type, bool isPrimary);
		Task<ApiResponse> DeleteAsync(Guid clientId, Guid phoneId);
	}
}