using ClientProfiles.Api.DTOs;
using ClientProfiles.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientProfiles.Api.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/client/address")]
	public class AddressController(IAddressService addressService, IClientService clientService) : BaseController
	{
		private readonly IAddressService _addressService = addressService;
		private readonly IClientService _clientService = clientService;

		[HttpGet]
		public async Task<IActionResult> GetByClientAsync()
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _addressService.GetByClientIdAsync(clientResponse.Data!.Id);
			return Ok(response);
		}

		[HttpPost]
		public async Task<IActionResult> CreateAsync([FromBody] CreateAddressRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _addressService.CreateAsync(
				clientResponse.Data!.Id, request.Street, request.City,
				request.State, request.PostalCode, request.Country, request.IsPrimary);
			return Ok(response);
		}

		[HttpPut("{addressId}")]
		public async Task<IActionResult> UpdateAsync(Guid addressId, [FromBody] UpdateAddressRequest request)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _addressService.UpdateAsync(
				clientResponse.Data!.Id, addressId, request.Street, request.City,
				request.State, request.PostalCode, request.Country, request.IsPrimary);
			return Ok(response);
		}

		[HttpDelete("{addressId}")]
		public async Task<IActionResult> DeleteAsync(Guid addressId)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await _clientService.GetByUserIdAsync(GetUserId()!);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await _addressService.DeleteAsync(clientResponse.Data!.Id, addressId);
			return Ok(response);
		}
	}
}