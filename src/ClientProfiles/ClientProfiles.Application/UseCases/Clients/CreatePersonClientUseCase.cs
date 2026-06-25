using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs;
using ClientProfiles.Application.DTOs.Responses;
using ClientProfiles.Domain;
using ClientProfiles.Domain.Entities;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Clients
{
	public sealed class CreatePersonClientUseCase(
		IClientRepository clientRepository,
		IPersonRepository personRepository,
		IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse<PersonResponse>> ExecuteAsync(string userId, CreatePersonRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var existing = await clientRepository.GetByUserIdAsync(userId, cancellationToken);
				if (existing != null)
					return ApiResponse<PersonResponse>.CreateFailure(Messages.Client.AlreadyExists);

				var client = Client.CreateClient(userId, ClientType.Person);
				await clientRepository.AddAsync(client, cancellationToken);

				var person = Person.CreatePerson(
					client.Id,
					request.FirstName,
					request.MiddleName,
					request.LastName,
					request.DateOfBirth,
					request.IdType,
					request.IdentificationNumber);
				await personRepository.AddAsync(person, cancellationToken);
				await unitOfWork.SaveChangesAsync(cancellationToken);

				return ApiResponse<PersonResponse>.CreateSuccess(person.Adapt<PersonResponse>(), Messages.Client.Created);
			}
			catch (Exception ex)
			{
				return ApiResponse<PersonResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
