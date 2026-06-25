using ClientProfiles.Application.DTOs;
using ClientProfiles.Application.UseCases.Clients;
using ClientProfiles.Application.UseCases.Phones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientProfiles.Api.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/client/phone")]
	public class PhoneController(
		GetClientByUserIdUseCase getClientByUserId,
		GetPhonesByClientUseCase getPhonesByClient,
		CreatePhoneUseCase createPhone,
		UpdatePhoneUseCase updatePhone,
		SetPrimaryPhoneUseCase setPrimaryPhone,
		DeletePhoneUseCase deletePhone) : BaseController
	{
		[HttpGet]
		public async Task<IActionResult> GetByClientAsync(CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null)	return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await getPhonesByClient.ExecuteAsync(clientResponse.Data!.Id, cancellationToken);
			return Ok(response);
		}

		[HttpPost]
		public async Task<IActionResult> CreateAsync([FromBody] CreatePhoneRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await createPhone.ExecuteAsync(clientResponse.Data!.Id, request, cancellationToken);
			return Ok(response);
		}

		[HttpPut("{phoneId}")]
		public async Task<IActionResult> UpdateAsync(Guid phoneId, [FromBody] UpdatePhoneRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await updatePhone.ExecuteAsync(clientResponse.Data!.Id, phoneId, request, cancellationToken);
			return Ok(response);
		}

		[HttpPut("{phoneId}/primary")]
		public async Task<IActionResult> SetPrimaryAsync(Guid phoneId, CancellationToken cancellationToken)
		{
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await setPrimaryPhone.ExecuteAsync(clientResponse.Data!.Id, phoneId, cancellationToken);
			return Ok(response);
		}

		[HttpDelete("{phoneId}")]
		public async Task<IActionResult> DeleteAsync(Guid phoneId, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null)	return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await deletePhone.ExecuteAsync(clientResponse.Data!.Id, phoneId, cancellationToken);
			return Ok(response);
		}
	}
}
