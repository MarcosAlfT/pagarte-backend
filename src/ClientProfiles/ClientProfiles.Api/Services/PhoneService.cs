using ClientProfiles.Api.Constants;
using ClientProfiles.Api.Domain;
using ClientProfiles.Api.Domain.Entities;
using ClientProfiles.Api.Interfaces;
using Utilities.Responses;
using ClientProfiles.Api.DTOs.Responses;
using Mapster;

namespace ClientProfiles.Api.Services
{
	public class PhoneService(IPhoneRepository phoneRepository) : IPhoneService
	{
		private readonly IPhoneRepository _phoneRepository = phoneRepository;

		public async Task<ApiResponse<IEnumerable<PhoneResponse>>> GetByClientIdAsync(Guid clientId)
		{
			try
			{
				var phones = await _phoneRepository.GetByClientIdAsync(clientId);
				return ApiResponse<IEnumerable<PhoneResponse>>.CreateSuccess(phones.Adapt<IEnumerable<PhoneResponse>>());
			}
			catch (Exception ex)
			{
				return ApiResponse<IEnumerable<PhoneResponse>>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse<PhoneResponse>> CreateAsync(Guid clientId, string number, PhoneType type, bool isPrimary)
		{
			try
			{
				if (isPrimary)
				{
					var existingPhones = await _phoneRepository.GetByClientIdAsync(clientId);
					if (existingPhones.Any(p => p.IsPrimary))
					{
						return ApiResponse<PhoneResponse>.CreateFailure(Messages.Phone.PrimaryPhoneExists);
					}
				}
				var phone = Phone.CreatePhone(clientId, number, type, isPrimary);
				await _phoneRepository.CreateAsync(phone);
				return ApiResponse<PhoneResponse>.CreateSuccess(phone.Adapt<PhoneResponse>(), Messages.Phone.Created);
			}
			catch (Exception ex)
			{
				return ApiResponse<PhoneResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse> UpdateAsync(Guid clientId, Guid phoneId, string number, PhoneType type, bool isPrimary)
		{
			try
			{
				var phones = await _phoneRepository.GetByClientIdAsync(clientId);
				var phone = phones.FirstOrDefault(p => p.Id == phoneId);
				if (phone == null)
					return ApiResponse.CreateFailure(Messages.Phone.NotFound);

				phone.UpdatePhone(number, type, isPrimary);
				await _phoneRepository.UpdateAsync(phone);
				return ApiResponse.CreateSuccess(Messages.Phone.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}

		public async Task<ApiResponse> DeleteAsync(Guid clientId, Guid phoneId)
		{
			try
			{
				await _phoneRepository.DeleteAsync(clientId, phoneId);
				return ApiResponse.CreateSuccess(Messages.Phone.Deleted);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}