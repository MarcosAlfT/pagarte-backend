using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;
using Grpc.Core;

namespace PaymentSwitch.Processor.GrpcServices
{
	public class CreditCardGrpcService(
		GetCreditCardsUseCase getCreditCardsUseCase,
		GetCreditCardUseCase getCreditCardUseCase,
		RegisterCreditCardUseCase registerCreditCardUseCase,
		UpdateCreditCardUseCase updateCreditCardUseCase,
		DeleteCreditCardUseCase deleteCreditCardUseCase)
		: PaymentSwitch.Contracts.CreditCardService.CreditCardServiceBase
	{
		private readonly GetCreditCardsUseCase _getCreditCardsUseCase =
			getCreditCardsUseCase;
		private readonly GetCreditCardUseCase _getCreditCardUseCase =
			getCreditCardUseCase;
		private readonly RegisterCreditCardUseCase _registerCreditCardUseCase =
			registerCreditCardUseCase;
		private readonly UpdateCreditCardUseCase _updateCreditCardUseCase =
			updateCreditCardUseCase;
		private readonly DeleteCreditCardUseCase _deleteCreditCardUseCase =
			deleteCreditCardUseCase;

		public override async Task<GetCardsResponse> GetCards(
			GetCardsRequest request, ServerCallContext context)
		{
			var cards = await _getCreditCardsUseCase.ExecuteAsync(request.ClientId);
			var response = new GetCardsResponse();
			response.Cards.AddRange(cards.Select(MapCard));
			return response;
		}

		public override async Task<GetCardResponse> GetCard(
			GetCardRequest request, ServerCallContext context)
		{
			var card = await _getCreditCardUseCase.ExecuteAsync(
				request.ClientId,
				Guid.Parse(request.CardId));

			if (card == null)
			{
				return new GetCardResponse { Found = false };
			}

			return new GetCardResponse { Found = true, Card = MapCard(card) };
		}

		public override async Task<RegisterCardResponse> RegisterCard(
			RegisterCardRequest request, ServerCallContext context)
		{
			var result = await _registerCreditCardUseCase.ExecuteAsync(
				new RegisterCreditCardCommand(
					request.ClientId,
					request.CardNumber,
					request.Cvv,
					request.CardHolderName,
					request.ExpiryMonth,
					request.ExpiryYear,
					request.IsDefault),
				context.CancellationToken);

			if (!result.Success)
			{
				return new RegisterCardResponse
				{
					Success = false,
					ErrorMessage = result.ErrorMessage ?? "Card registration failed."
				};
			}

			var card = result.Card!;

			return new RegisterCardResponse
			{
				Success = true,
				CardId = card.Id.ToString(),
				Last4Digits = card.Last4Digits,
				CardType = result.CardType ?? card.CardType.ToString(),
				ExpiryMonth = card.ExpiryMonth,
				ExpiryYear = card.ExpiryYear
			};
		}

		public override async Task<MutationResponse> UpdateCard(
			UpdateCardRequest request, ServerCallContext context)
		{
			var errorMessage = await _updateCreditCardUseCase.ExecuteAsync(
				new UpdateCreditCardCommand(
					request.ClientId,
					Guid.Parse(request.CardId),
					request.CardHolderName,
					request.IsDefault),
				context.CancellationToken);

			if (errorMessage != null)
			{
				return new MutationResponse
				{
					Success = false,
					ErrorMessage = errorMessage
				};
			}

			return new MutationResponse { Success = true };
		}

		public override async Task<MutationResponse> DeleteCard(
			DeleteCardRequest request, ServerCallContext context)
		{
			await _deleteCreditCardUseCase.ExecuteAsync(
				new DeleteCreditCardCommand(
					request.ClientId,
					Guid.Parse(request.CardId)),
				context.CancellationToken);

			return new MutationResponse { Success = true };
		}

		private static CreditCardDto MapCard(Domain.Entities.CreditCard card) =>
			new()
			{
				Id = card.Id.ToString(),
				CardHolderName = card.CardHolderName,
				Last4Digits = card.Last4Digits,
				CardType = card.CardType.ToString(),
				ExpiryMonth = card.ExpiryMonth,
				ExpiryYear = card.ExpiryYear,
				IsDefault = card.IsDefault,
				CreatedAt = card.CreatedAt.ToString("O"),
				OperatorProvider = card.OperatorProvider
			};
	}
}
