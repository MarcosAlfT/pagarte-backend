using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.DTOs.Responses;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Phones
{
	public sealed class GetPhonesByClientUseCase(IPhoneRepository phoneRepository)
	{
		public async Task<ApiResponse<IEnumerable<PhoneResponse>>> ExecuteAsync(Guid clientId, CancellationToken cancellationToken)
		{
			try
			{
				var phones = await phoneRepository.GetByClientIdAsync(clientId, cancellationToken);
				return ApiResponse<IEnumerable<PhoneResponse>>.CreateSuccess(phones.Adapt<IEnumerable<PhoneResponse>>());
			}
			catch (Exception ex)
			{
				return ApiResponse<IEnumerable<PhoneResponse>>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
