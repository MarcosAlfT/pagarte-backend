using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.DTOs.Responses;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Clients
{
	public sealed class GetClientsUseCase(IClientRepository clientRepository)
	{
		public async Task<ApiResponse<IEnumerable<ClientResponse>>> ExecuteAsync(CancellationToken cancellationToken)
		{
			try
			{
				var clients = await clientRepository.GetAllAsync(cancellationToken);
				return ApiResponse<IEnumerable<ClientResponse>>.CreateSuccess(clients.Adapt<IEnumerable<ClientResponse>>());
			}
			catch (Exception ex)
			{
				return ApiResponse<IEnumerable<ClientResponse>>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
