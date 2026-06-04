using ClientProfiles.Api.DTOs;
using ClientProfiles.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Utilities.Responses;

namespace ClientProfiles.Api.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/client")]
	public class ClientController(IClientService clientService) : BaseController
	{
		private readonly IClientService _clientService = clientService;

		[HttpGet]
		public async Task<IActionResult> GetAsync()
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var response = await _clientService.GetByUserIdAsync(GetUserId()!);
			return Ok(response);
		}

		[HttpPost("person")]
		public async Task<IActionResult> CreatePersonAsync([FromBody] CreatePersonRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var response = await _clientService.CreatePersonClientAsync(
				GetUserId()!, request.FirstName, request.MiddleName,
				request.LastName, request.DateOfBirth, request.IdType,
				request.IdentificationNumber);
			return Ok(response);
		}


		[HttpPut("person")]
		public async Task<IActionResult> UpdatePersonAsync([FromBody] UpdatePersonRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _clientService.UpdatePersonAsync(
				clientResponse.Data!.Id, request.FirstName, request.MiddleName,
				request.LastName, request.DateOfBirth, request.IdType,
				request.IdentificationNumber);
			return Ok(response);
		}

		[HttpPost("organization")]
		public async Task<IActionResult> CreateOrganizationAsync([FromBody] CreateOrganizationRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var response = await _clientService.CreateOrganizationClientAsync(
				GetUserId()!, request.Name, request.Industry,
				request.IdentificationNumber);
			return Ok(response);
		}


		[HttpPut("organization")]
		public async Task<IActionResult> UpdateOrganizationAsync([FromBody] UpdateOrganizationRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _clientService.UpdateOrganizationAsync(
				clientResponse.Data!.Id, request.Name, request.Industry,
				request.IdentificationNumber);
			return Ok(response);
		}

		[HttpDelete]
		public async Task<IActionResult> DeleteAsync()
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _clientService.DeleteAsync(clientResponse.Data!.Id);
			return Ok(response);
		}
	}
}