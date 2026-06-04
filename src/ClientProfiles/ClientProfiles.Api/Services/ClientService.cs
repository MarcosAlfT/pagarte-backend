using ClientProfiles.Api.Domain;
using ClientProfiles.Api.DTOs.Responses;
using ClientProfiles.Api.Domain.Entities;
using ClientProfiles.Api.Interfaces;
using Utilities.Responses;
using ClientProfiles.Api.Constants;
using Mapster;

namespace ClientProfiles.Api.Services
{
	public class ClientService(IClientRepository clientRepository, IPersonRepository personRepository, IOrganizationRepository organizationRepository) : IClientService
	{
		private readonly IClientRepository _clientRepository = clientRepository;
		private readonly IPersonRepository _personRepository = personRepository;
		private readonly IOrganizationRepository _organizationRepository = organizationRepository;

		public async Task<ApiResponse<ClientResponse>> GetByUserIdAsync(string userId)
		{
			try
			{
				var client = await _clientRepository.GetByUserIdAsync(userId);
				if (client == null)
					return ApiResponse<ClientResponse>.CreateFailure(Messages.Client.NotFound);

				return ApiResponse<ClientResponse>.CreateSuccess(client.Adapt<ClientResponse>());
			}
			catch (Exception ex)
			{
				return ApiResponse<ClientResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse<IEnumerable<ClientResponse>>> GetAllAsync()
		{
			try
			{
				var clients = await _clientRepository.GetAllAsync();
				return ApiResponse<IEnumerable<ClientResponse>>.CreateSuccess(clients.Adapt<IEnumerable<ClientResponse>>());
			}
			catch (Exception ex)
			{
				return ApiResponse<IEnumerable<ClientResponse>>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse<PersonResponse>> CreatePersonClientAsync(string userId, string firstName, string? middleName, string lastName, DateTime dateOfBirth, IdentificationType idType, string identificationNumber)
		{
			try
			{
				var existing = await _clientRepository.GetByUserIdAsync(userId);
				if (existing != null)
					return ApiResponse<PersonResponse>.CreateFailure(Messages.Client.AlreadyExists);

				var client = Client.CreateClient(userId, ClientType.Person);
				await _clientRepository.CreateAsync(client);

				var person = Person.CreatePerson(client.Id, firstName, middleName, lastName, dateOfBirth, idType, identificationNumber);
				await _personRepository.CreateAsync(person);

				return ApiResponse<PersonResponse>.CreateSuccess(person.Adapt<PersonResponse>(), Messages.Client.Created);
			}
			catch (Exception ex)
			{
				return ApiResponse<PersonResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse<OrganizationResponse>> CreateOrganizationClientAsync(string userId, string name, IndustryType industry, string identificationNumber)
		{
			try
			{
				var existing = await _clientRepository.GetByUserIdAsync(userId);
				if (existing != null)
					return ApiResponse<OrganizationResponse>.CreateFailure(Messages.Client.AlreadyExists);

				var client = Client.CreateClient(userId, ClientType.Organization);
				await _clientRepository.CreateAsync(client);

				var organization = Organization.CreateOrganization(client.Id, name, industry, identificationNumber);
				await _organizationRepository.CreateAsync(organization);

				return ApiResponse<OrganizationResponse>.CreateSuccess(organization.Adapt<OrganizationResponse>(), Messages.Client.Created);
			}
			catch (Exception ex)
			{
				return ApiResponse<OrganizationResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse> UpdatePersonAsync(Guid clientId, string firstName, string? middleName, string lastName, DateTime dateOfBirth, IdentificationType idType, string identificationNumber)
		{
			try
			{
				var error = await GetAndValidateClientAsync(clientId, ClientType.Person);
				if (!error.Success)
					return error;

				var person = await _personRepository.GetByClientIdAsync(clientId);
				if (person == null)
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				person.UpdatePerson(firstName, middleName, lastName, dateOfBirth, idType, identificationNumber);
				await _personRepository.UpdateAsync(person);

				return ApiResponse.CreateSuccess(Messages.Client.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse> UpdateOrganizationAsync(Guid clientId, string name, IndustryType industry, string identificationNumber)
		{
			try
			{
				var error = await GetAndValidateClientAsync(clientId, ClientType.Person);
				if (!error.Success)
					return error;

				var organization = await _organizationRepository.GetByClientIdAsync(clientId);
				if (organization == null)	
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				organization.UpdateOrganization(name, industry, identificationNumber);
				await _organizationRepository.UpdateAsync(organization);

				return ApiResponse.CreateSuccess(Messages.Client.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse> DeleteAsync(Guid id)
		{
			try
			{
				var client = await _clientRepository.GetByClientIdAsync(id);
				if (client == null)
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				await _clientRepository.DeleteAsync(id);
				return ApiResponse.CreateSuccess(Messages.Client.Deleted);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
		private async Task<ApiResponse> GetAndValidateClientAsync(Guid clientId, ClientType expectedType)
		{
			var client = await _clientRepository.GetByClientIdAsync(clientId);
			if (client == null)
				return ApiResponse.CreateFailure(Messages.Client.NotFound);

			if (client.Type != expectedType)
				return ApiResponse.CreateFailure(
					expectedType == ClientType.Person ?
					Messages.Client.NotPerson :
					Messages.Client.NotOrganization);

			return ApiResponse.CreateSuccess();
		}
	}
}