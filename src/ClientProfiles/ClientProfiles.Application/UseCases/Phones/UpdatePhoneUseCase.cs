using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Phones
{
	public sealed class UpdatePhoneUseCase(IPhoneRepository phoneRepository, IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, Guid phoneId, UpdatePhoneRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var phones = (await phoneRepository.GetByClientIdAsync(clientId, cancellationToken)).ToList();
				var phone = phones.FirstOrDefault(item => item.Id == phoneId);
				if (phone == null)
					return ApiResponse.CreateFailure(Messages.Phone.NotFound);

				phone.UpdatePhone(request.Number, request.Type);
				await unitOfWork.SaveChangesAsync(cancellationToken);

				return ApiResponse.CreateSuccess(Messages.Phone.Updated);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
