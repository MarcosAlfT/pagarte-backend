using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Payments.Api.DTOs;
using Payments.Api.GrpcClients;
using Utilities.Responses;

namespace Payments.Api.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/creditcard")]
	public class CreditCardController(CreditCardGrpcClient grpcClient) : BaseController
	{
		private readonly CreditCardGrpcClient _grpcClient = grpcClient;

		[HttpGet]
		public async Task<IActionResult> GetAsync()
		{
			var validation = ValidateClientId();
			if (validation != null) return validation;

			var result = await _grpcClient.GetCardsAsync(GetClientId()!);
			return Ok(ApiResponse<object>.CreateSuccess(result.Cards));
		}

		[HttpGet("{cardId}")]
		public async Task<IActionResult> GetByIdAsync(string cardId)
		{
			var validation = ValidateClientId();
			if (validation != null) return validation;

			var result = await _grpcClient.GetCardAsync(cardId, GetClientId()!);
			if (!result.Found)
				return Ok(ApiResponse.CreateFailure("Credit card not found."));

			return Ok(ApiResponse<object>.CreateSuccess(result.Card));
		}

		[HttpPost]
		public async Task<IActionResult> RegisterAsync(
			[FromBody] RegisterCreditCardRequest request)
		{
			var validation = ValidateClientId();
			if (validation != null) return validation;

			var result = await _grpcClient.RegisterCardAsync(
				GetClientId()!, request.CardNumber, request.Cvv, request.CardHolderName,
				request.ExpiryMonth, request.ExpiryYear, request.IsDefault);

			if (!result.Success)
				return Ok(ApiResponse.CreateFailure(result.ErrorMessage));

			return Ok(ApiResponse<object>.CreateSuccess(new
			{
				result.CardId,
				result.Last4Digits,
				result.CardType,
				result.ExpiryMonth,
				result.ExpiryYear
			}, "Card registered successfully."));
		}

		[HttpPut("{cardId}")]
		public async Task<IActionResult> UpdateAsync(string cardId,
			[FromBody] UpdateCreditCardRequest request)
		{
			var validation = ValidateClientId();
			if (validation != null) return validation;

			var result = await _grpcClient.UpdateCardAsync(
				cardId, GetClientId()!,
				request.CardHolderName, request.IsDefault);

			if (!result.Success)
				return Ok(ApiResponse.CreateFailure(result.ErrorMessage));

			return Ok(ApiResponse.CreateSuccess("Card updated successfully."));
		}

		[HttpDelete("{cardId}")]
		public async Task<IActionResult> DeleteAsync(string cardId)
		{
			var validation = ValidateClientId();
			if (validation != null) return validation;

			var result = await _grpcClient.DeleteCardAsync(cardId, GetClientId()!);

			if (!result.Success)
				return Ok(ApiResponse.CreateFailure(result.ErrorMessage));

			return Ok(ApiResponse.CreateSuccess("Card deleted successfully."));
		}
	}
}
