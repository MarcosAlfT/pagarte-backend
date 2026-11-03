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
	public sealed class CreateOrganizationClientUseCase(
		IClientRepository clientRepository,
		IOrganizationRepository organizationRepository,
		IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse<OrganizationResponse>> ExecuteAsync(string issuer, string subject, CreateOrganizationRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var existing = await clientRepository.GetByExternalIdentityAsync(issuer, subject, cancellationToken);
				if (existing != null)
					return ApiResponse<OrganizationResponse>.CreateFailure(Messages.Client.AlreadyExists);

				var client = Client.CreateClient(issuer, subject, ClientType.Organization);
				await clientRepository.AddAsync(client, cancellationToken);

				var organization = Organization.CreateOrganization(client.Id, request.Name, request.Industry, request.IdentificationNumber);
				await organizationRepository.AddAsync(organization, cancellationToken);
				await unitOfWork.SaveChangesAsync(cancellationToken);

				return ApiResponse<OrganizationResponse>.CreateSuccess(organization.Adapt<OrganizationResponse>(), Messages.Client.Created);
			}
			catch (Exception)
			{
				return ApiResponse<OrganizationResponse>.CreateFailure("Client profile could not be created.");
			}
		}
	}
}
