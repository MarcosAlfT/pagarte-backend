using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Addresses
{
	public sealed class DeleteAddressUseCase(IAddressRepository addressRepository, IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken)
		{
			try
			{
				var addresses = await addressRepository.GetByClientIdAsync(clientId, cancellationToken);
				var address = addresses.FirstOrDefault(item => item.Id == addressId);
				if (address == null)
					return ApiResponse.CreateFailure(Messages.Address.NotFound);

				address.DeleteAddress();
				await unitOfWork.SaveChangesAsync(cancellationToken);
				return ApiResponse.CreateSuccess(Messages.Address.Deleted);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
