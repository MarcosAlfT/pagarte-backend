using ClientProfiles.Api.DTOs;
using ClientProfiles.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientProfiles.Api.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/client/phone")]
	public class PhoneController(IPhoneService phoneService, IClientService clientService) : BaseController
	{
		private readonly IPhoneService _phoneService = phoneService;
		private readonly IClientService _clientService = clientService;

		[HttpGet]
		public async Task<IActionResult> GetByClientAsync()
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null)	return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _phoneService.GetByClientIdAsync(clientResponse.Data!.Id);
			return Ok(response);
		}

		[HttpPost]
		public async Task<IActionResult> CreateAsync([FromBody] CreatePhoneRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _phoneService.CreateAsync(
				clientResponse.Data!.Id, request.Number, request.Type, request.IsPrimary);
			return Ok(response);
		}

		[HttpPut("{phoneId}")]
		public async Task<IActionResult> UpdateAsync(Guid phoneId, [FromBody] UpdatePhoneRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _phoneService.UpdateAsync(
				clientResponse.Data!.Id, phoneId, request.Number, request.Type, request.IsPrimary);
			return Ok(response);
		}

		[HttpDelete("{phoneId}")]
		public async Task<IActionResult> DeleteAsync(Guid phoneId)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null)	return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _phoneService.DeleteAsync(clientResponse.Data!.Id, phoneId);
			return Ok(response);
		}
	}
}