using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs.Responses;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Clients
{
	public sealed class GetClientByExternalIdentityUseCase(IClientRepository clientRepository)
	{
		public async Task<ApiResponse<ClientResponse>> ExecuteAsync(string issuer, string subject, CancellationToken cancellationToken)
		{
			try
			{
				var client = await clientRepository.GetByExternalIdentityAsync(issuer, subject, cancellationToken);
				if (client == null)
					return ApiResponse<ClientResponse>.CreateFailure(Messages.Client.NotFound);

				return ApiResponse<ClientResponse>.CreateSuccess(client.Adapt<ClientResponse>());
			}
			catch (Exception)
			{
				return ApiResponse<ClientResponse>.CreateFailure("Client profile could not be loaded.");
			}
		}
	}
}
