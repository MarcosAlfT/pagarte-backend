using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs;
using ClientProfiles.Application.DTOs.Responses;
using ClientProfiles.Domain.Entities;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Addresses
{
	public sealed class CreateAddressUseCase(IAddressRepository addressRepository, IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse<AddressResponse>> ExecuteAsync(Guid clientId, CreateAddressRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var existingAddresses = (await addressRepository.GetByClientIdAsync(clientId, cancellationToken)).ToList();
				var shouldBePrimary = existingAddresses.Count == 0;

				var address = Address.CreateAddress(
					clientId,
					request.Street,
					request.City,
					request.State,
					request.PostalCode,
					request.Country,
					shouldBePrimary);
				await addressRepository.AddAsync(address, cancellationToken);
				await unitOfWork.SaveChangesAsync(cancellationToken);

				return ApiResponse<AddressResponse>.CreateSuccess(address.Adapt<AddressResponse>(), Messages.Address.Created);
			}
			catch (Exception ex)
			{
				return ApiResponse<AddressResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
