using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Clients
{
	public sealed class DeleteClientUseCase(IClientRepository clientRepository, IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, CancellationToken cancellationToken)
		{
			try
			{
				var client = await clientRepository.GetByClientIdAsync(clientId, cancellationToken);
				if (client == null)
					return ApiResponse.CreateFailure(Messages.Client.NotFound);

				client.Delete();
				await unitOfWork.SaveChangesAsync(cancellationToken);
				return ApiResponse.CreateSuccess(Messages.Client.Deleted);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
