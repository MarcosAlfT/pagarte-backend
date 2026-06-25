using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Addresses
{
	public sealed class SetPrimaryAddressUseCase(IAddressRepository addressRepository)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken)
		{
			try
			{
				var addresses = (await addressRepository.GetByClientIdAsync(clientId, cancellationToken)).ToList();
				var address = addresses.FirstOrDefault(item => item.Id == addressId);
				if (address == null)
					return ApiResponse.CreateFailure(Messages.Address.NotFound);

				await addressRepository.SetPrimaryAddressAsync(clientId, addressId, cancellationToken);

				return ApiResponse.CreateSuccess(Messages.Address.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
