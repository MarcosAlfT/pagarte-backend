using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.DTOs.Responses;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Addresses
{
	public sealed class GetAddressesByClientUseCase(IAddressRepository addressRepository)
	{
		public async Task<ApiResponse<IEnumerable<AddressResponse>>> ExecuteAsync(Guid clientId, CancellationToken cancellationToken)
		{
			try
			{
				var addresses = await addressRepository.GetByClientIdAsync(clientId, cancellationToken);
				return ApiResponse<IEnumerable<AddressResponse>>.CreateSuccess(addresses.Adapt<IEnumerable<AddressResponse>>());
			}
			catch (Exception ex)
			{
				return ApiResponse<IEnumerable<AddressResponse>>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
