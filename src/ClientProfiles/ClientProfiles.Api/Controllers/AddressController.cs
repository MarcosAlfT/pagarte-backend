using ClientProfiles.Application.DTOs;
using ClientProfiles.Application.UseCases.Addresses;
using ClientProfiles.Application.UseCases.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClientProfiles.Api.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/client/address")]
	public class AddressController(
		GetClientByUserIdUseCase getClientByUserId,
		GetAddressesByClientUseCase getAddressesByClient,
		CreateAddressUseCase createAddress,
		UpdateAddressUseCase updateAddress,
		SetPrimaryAddressUseCase setPrimaryAddress,
		DeleteAddressUseCase deleteAddress) : BaseController
	{
		[HttpGet]
		public async Task<IActionResult> GetByClientAsync(CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await getAddressesByClient.ExecuteAsync(clientResponse.Data!.Id, cancellationToken);
			return Ok(response);
		}

		[HttpPost]
		public async Task<IActionResult> CreateAsync([FromBody] CreateAddressRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await createAddress.ExecuteAsync(clientResponse.Data!.Id, request, cancellationToken);
			return Ok(response);
		}

		[HttpPut("{addressId}")]
		public async Task<IActionResult> UpdateAsync(Guid addressId, [FromBody] UpdateAddressRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await updateAddress.ExecuteAsync(clientResponse.Data!.Id, addressId, request, cancellationToken);
			return Ok(response);
		}

		[HttpPut("{addressId}/primary")]
		public async Task<IActionResult> SetPrimaryAsync(Guid addressId, CancellationToken cancellationToken)
		{
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await setPrimaryAddress.ExecuteAsync(clientResponse.Data!.Id, addressId, cancellationToken);
			return Ok(response);
		}

		[HttpDelete("{addressId}")]
		public async Task<IActionResult> DeleteAsync(Guid addressId, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateUserId();
			if (validation != null) return validation;

			var clientResponse = await getClientByUserId.ExecuteAsync(GetUserId()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await deleteAddress.ExecuteAsync(clientResponse.Data!.Id, addressId, cancellationToken);
			return Ok(response);
		}
	}
}
