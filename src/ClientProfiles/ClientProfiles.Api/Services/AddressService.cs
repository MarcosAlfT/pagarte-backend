using ClientProfiles.Api.Constants;
using ClientProfiles.Api.Domain.Entities;
using ClientProfiles.Api.Interfaces;
using Utilities.Responses;
using ClientProfiles.Api.DTOs.Responses;
using Mapster;

namespace ClientProfiles.Api.Services
{
	public class AddressService(IAddressRepository addressRepository) : IAddressService
	{
		private readonly IAddressRepository _addressRepository = addressRepository;

		public async Task<ApiResponse<IEnumerable<AddressResponse>>> GetByClientIdAsync(Guid clientId)
		{
			try
			{
				var addresses = await _addressRepository.GetByClientIdAsync(clientId);
				return ApiResponse<IEnumerable<AddressResponse>>.CreateSuccess(addresses.Adapt<IEnumerable<AddressResponse>>());
			}
			catch (Exception ex)
			{
				return ApiResponse<IEnumerable<AddressResponse>>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse<AddressResponse>> CreateAsync(Guid clientId, string street, string city, string state, string postalCode, string country, bool isPrimary)
		{
			try
			{
				if (isPrimary)
				{
					var existingAddresses = await _addressRepository.GetByClientIdAsync(clientId);
					if (existingAddresses.Any(a => a.IsPrimary))
					{
						return ApiResponse<AddressResponse>.CreateFailure(Messages.Address.PrimaryAddressExists);
					}
				}

				var address = Address.CreateAddress(clientId, street, city, state, postalCode, country, isPrimary);
				await _addressRepository.CreateAsync(address);
				return ApiResponse<AddressResponse>.CreateSuccess(address.Adapt<AddressResponse>(), Messages.Address.Created);
			}
			catch (Exception ex)
			{
				return ApiResponse<AddressResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse> UpdateAsync(Guid clientId, Guid addressId, string street, string city, string state, string postalCode, string country, bool isPrimary)
		{
			try
			{
				var addresses = await _addressRepository.GetByClientIdAsync(clientId);
				var address = addresses.FirstOrDefault(a => a.Id == addressId);
				if (address == null)
					return ApiResponse.CreateFailure(Messages.Address.NotFound);

				address.UpdateAddress(street, city, state, postalCode, country, isPrimary);
				await _addressRepository.UpdateAsync(address);
				return ApiResponse.CreateSuccess(Messages.Address.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse> DeleteAsync(Guid clientId, Guid addressId)
		{
			try
			{
				await _addressRepository.DeleteAsync(clientId, addressId);
				return ApiResponse.CreateSuccess(Messages.Address.Deleted);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
