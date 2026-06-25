using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs;
using ClientProfiles.Domain;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Clients
{
	public sealed class UpdateOrganizationClientUseCase(
		IClientRepository clientRepository,
		IOrganizationRepository organizationRepository,
		IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, UpdateOrganizationRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var client = await clientRepository.GetByClientIdAsync(clientId, cancellationToken);
				if (client == null)
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				if (client.Type != ClientType.Organization)
					return ApiResponse.CreateFailure(Messages.Client.NotOrganization);

				var organization = await organizationRepository.GetByClientIdAsync(clientId, cancellationToken);
				if (organization == null)
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				organization.UpdateOrganization(request.Name, request.Industry, request.IdentificationNumber);
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
