using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Phones
{
	public sealed class DeletePhoneUseCase(IPhoneRepository phoneRepository, IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse> ExecuteAsync(Guid clientId, Guid phoneId, CancellationToken cancellationToken)
		{
			try
			{
				var phones = await phoneRepository.GetByClientIdAsync(clientId, cancellationToken);
				var phone = phones.FirstOrDefault(item => item.Id == phoneId);
				if (phone == null)
					return ApiResponse.CreateFailure(Messages.Phone.NotFound);

				phone.DeletePhone();
				await unitOfWork.SaveChangesAsync(cancellationToken);
				return ApiResponse.CreateSuccess(Messages.Phone.Deleted);
			}
			catch (Exception ex)
			{
				return ApiResponse.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
