using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs;
using ClientProfiles.Domain;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Clients
{
	public sealed class UpdatePersonClientUseCase(
		IClientRepository clientRepository,
		IPersonRepository personRepository,
		IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, UpdatePersonRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var client = await clientRepository.GetByClientIdAsync(clientId, cancellationToken);
				if (client == null)
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				if (client.Type != ClientType.Person)
					return ApiResponse.CreateFailure(Messages.Client.NotPerson);

				var person = await personRepository.GetByClientIdAsync(clientId, cancellationToken);
				if (person == null)
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				person.UpdatePerson(
					request.FirstName,
					request.MiddleName,
					request.LastName,
					request.DateOfBirth,
					request.IdType,
					request.IdentificationNumber);
				await unitOfWork.SaveChangesAsync(cancellationToken);

				return ApiResponse.CreateSuccess(Messages.Client.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
