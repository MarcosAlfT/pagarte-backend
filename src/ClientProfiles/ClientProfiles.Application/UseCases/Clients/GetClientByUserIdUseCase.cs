using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs.Responses;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Clients
{
	public sealed class GetClientByUserIdUseCase(IClientRepository clientRepository)
	{
		public async Task<ApiResponse<ClientResponse>> ExecuteAsync(string userId, CancellationToken cancellationToken)
		{
			try
			{
				var client = await clientRepository.GetByUserIdAsync(userId, cancellationToken);
				if (client == null)
					return ApiResponse<ClientResponse>.CreateFailure(Messages.Client.NotFound);

				return ApiResponse<ClientResponse>.CreateSuccess(client.Adapt<ClientResponse>());
			}
			catch (Exception ex)
			{
				return ApiResponse<ClientResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
