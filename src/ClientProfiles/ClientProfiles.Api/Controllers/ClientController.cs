using ClientProfiles.Application.DTOs;
using ClientProfiles.Application.UseCases.Clients;
using ClientProfiles.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Utilities.Responses;

namespace ClientProfiles.Api.Controllers
{
	[ApiController]
	[Authorize(Policy = "VerifiedClient")]
	[Route("api/client")]
	public class ClientController(
		GetClientByExternalIdentityUseCase getClientByExternalIdentity,
		CreatePersonClientUseCase createPersonClient,
		CreateOrganizationClientUseCase createOrganizationClient,
		UpdatePersonClientUseCase updatePersonClient,
		UpdateOrganizationClientUseCase updateOrganizationClient,
		DeleteClientUseCase deleteClient) : BaseController
	{
		[HttpGet]
		public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateExternalIdentity();
			if (validation != null) return validation;

			var response = await getClientByExternalIdentity.ExecuteAsync(GetIssuer()!, GetSubject()!, cancellationToken);
			return Ok(response);
		}

		[HttpPost("person")]
		[AllowProfileCreation]
		public async Task<IActionResult> CreatePersonAsync([FromBody] CreatePersonRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateExternalIdentity();
			if (validation != null) return validation;

			var response = await createPersonClient.ExecuteAsync(GetIssuer()!, GetSubject()!, request, cancellationToken);
			return Ok(response);
		}


		[HttpPut("person")]
		public async Task<IActionResult> UpdatePersonAsync([FromBody] UpdatePersonRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateExternalIdentity();
			if (validation != null) return validation;

			var clientResponse = await getClientByExternalIdentity.ExecuteAsync(GetIssuer()!, GetSubject()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await updatePersonClient.ExecuteAsync(clientResponse.Data!.Id, request, cancellationToken);
			return Ok(response);
		}

		[HttpPost("organization")]
		[AllowProfileCreation]
		public async Task<IActionResult> CreateOrganizationAsync([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateExternalIdentity();
			if (validation != null) return validation;

			var response = await createOrganizationClient.ExecuteAsync(GetIssuer()!, GetSubject()!, request, cancellationToken);
			return Ok(response);
		}


		[HttpPut("organization")]
		public async Task<IActionResult> UpdateOrganizationAsync([FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateExternalIdentity();
			if (validation != null) return validation;

			var clientResponse = await getClientByExternalIdentity.ExecuteAsync(GetIssuer()!, GetSubject()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await updateOrganizationClient.ExecuteAsync(clientResponse.Data!.Id, request, cancellationToken);
			return Ok(response);
		}

		[HttpDelete]
		public async Task<IActionResult> DeleteAsync(CancellationToken cancellationToken)
		{
			// Validate user ID return null if valid, otherwise return an IActionResult with the error response
			var validation = ValidateExternalIdentity();
			if (validation != null) return validation;

			var clientResponse = await getClientByExternalIdentity.ExecuteAsync(GetIssuer()!, GetSubject()!, cancellationToken);
			if (!clientResponse.Success)
				return Ok(clientResponse);

			var response = await deleteClient.ExecuteAsync(clientResponse.Data!.Id, cancellationToken);
			return Ok(response);
		}
	}
}
