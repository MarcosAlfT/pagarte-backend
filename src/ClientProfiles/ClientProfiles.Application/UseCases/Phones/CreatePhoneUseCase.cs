using ClientProfiles.Application.Abstractions;
using ClientProfiles.Application.Constants;
using ClientProfiles.Application.DTOs;
using ClientProfiles.Application.DTOs.Responses;
using ClientProfiles.Domain.Entities;
using Mapster;
using Utilities.Responses;

namespace ClientProfiles.Application.UseCases.Phones
{
	public sealed class CreatePhoneUseCase(IPhoneRepository phoneRepository, IUnitOfWork unitOfWork)
	{
		public async Task<ApiResponse<PhoneResponse>> ExecuteAsync(Guid clientId, CreatePhoneRequest request, CancellationToken cancellationToken)
		{
			try
			{
				var existingPhones = (await phoneRepository.GetByClientIdAsync(clientId, cancellationToken)).ToList();
				var shouldBePrimary = existingPhones.Count == 0;

				var phone = Phone.CreatePhone(clientId, request.Number, request.Type, shouldBePrimary);
				await phoneRepository.AddAsync(phone, cancellationToken);
				await unitOfWork.SaveChangesAsync(cancellationToken);

				return ApiResponse<PhoneResponse>.CreateSuccess(phone.Adapt<PhoneResponse>(), Messages.Phone.Created);
			}
			catch (Exception ex)
			{
				return ApiResponse<PhoneResponse>.CreateFailure($"An error occurred: {ex.Message}");
			}
		}
	}
}
