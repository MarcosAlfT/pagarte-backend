using ClientProfiles.Api.Domain;
using ClientProfiles.Api.DTOs.Responses;
using Utilities.Responses;

namespace ClientProfiles.Api.Interfaces
{
	public interface IClientService
	{
		Task<ApiResponse<ClientResponse>> GetByUserIdAsync(string userId);
		Task<ApiResponse<IEnumerable<ClientResponse>>> GetAllAsync();
		Task<ApiResponse<PersonResponse>> CreatePersonClientAsync(string userId, string firstName, string? middleName, string lastName, DateTime dateOfBirth, IdentificationType idType, string identificationNumber);
		Task<ApiResponse<OrganizationResponse>> CreateOrganizationClientAsync(string userId, string name, IndustryType industry, string identificationNumber);
		Task<ApiResponse> UpdatePersonAsync(Guid clientId, string firstName, string? middleName, string lastName, DateTime dateOfBirth, IdentificationType idType, string identificationNumber);
		Task<ApiResponse> UpdateOrganizationAsync(Guid clientId, string name, IndustryType industry, string identificationNumber);
		Task<ApiResponse> DeleteAsync(Guid id);
	}
}