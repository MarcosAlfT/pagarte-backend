using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Addresses
{
	public sealed class UpdateAddressUseCase(IAddressRepository addressRepository, IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, Guid addressId, UpdateAddressRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var addresses = (await addressRepository.GetByClientIdAsync(clientId, cancellationToken)).ToList();
				var address = addresses.FirstOrDefault(item => item.Id == addressId);
				if (address == null)
					return ApiResponse.CreateFailure(Messages.Address.NotFound);

				address.UpdateAddress(
					request.Street,
					request.City,
					request.State,
					request.PostalCode,
					request.Country);
				await unitOfWork.SaveChangesAsync(cancellationToken);

				return ApiResponse.CreateSuccess(Messages.Address.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
