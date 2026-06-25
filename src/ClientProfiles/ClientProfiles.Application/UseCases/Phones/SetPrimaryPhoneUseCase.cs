using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Phones
{
	public sealed class SetPrimaryPhoneUseCase(IPhoneRepository phoneRepository)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, Guid phoneId, CancellationToken cancellationToken)
		{
			try
			{
				var phones = (await phoneRepository.GetByClientIdAsync(clientId, cancellationToken)).ToList();
				var phone = phones.FirstOrDefault(item => item.Id == phoneId);
				if (phone == null)
					return ApiResponse.CreateFailure(Messages.Phone.NotFound);

				await phoneRepository.SetPrimaryPhoneAsync(clientId, phoneId, cancellationToken);

				return ApiResponse.CreateSuccess(Messages.Phone.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
