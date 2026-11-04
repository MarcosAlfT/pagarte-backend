using Grpc.Core;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Contracts;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;

namespace PaymentSwitch.Processor.GrpcServices;

public sealed class CreditCardGrpcService(GetCreditCardsUseCase list, GetCreditCardUseCase detail,
	UpdateCreditCardUseCase update, DeleteCreditCardUseCase delete, StartCardRegistrationUseCase start,
	CompleteCardRegistrationUseCase complete, GetCardRegistrationUseCase getRegistration,
	ContinueCardRegistrationUseCase continueRegistration, ReceiveCardSetupNotificationUseCase notifications, IClock clock, IOptions<CardRegistrationOptions> options, GetCardRemovalUseCase removal) : CreditCardService.CreditCardServiceBase
{
	public override Task<CardRemovalDto> GetCardRemoval(GetCardRequest request, ServerCallContext context) => Run(async () =>
	{
		var view = await removal.ExecuteAsync(Client(request.ClientId), Id(request.CardId), context.CancellationToken);
		return new CardRemovalDto { Id = view.Id.ToString(), IsDeleted = view.IsDeleted, ProviderRemovalState = view.ProviderRemovalState, NextAttemptAt = view.NextAttemptAt?.ToString("O") ?? "" };
	});

	public override Task<CardRegistrationPolicyDto> GetCardRegistrationPolicy(CardRegistrationPolicyRequest request, ServerCallContext context) =>
		Task.FromResult(new CardRegistrationPolicyDto { ConsentVersion = options.Value.ConsentVersion, Purpose = "CustomerInitiatedCardPayments" });

	public override Task<MutationResponse> ReceiveCardSetupNotification(CardSetupNotificationRequest request, ServerCallContext context) => Run(async () =>
	{
		if (request.Payload.Length > 8192 || request.IntegrationId.Length > 100) throw new CardOperationException("INVALID_PROVIDER_CALLBACK", "Invalid callback.", 401);
		if (request.Headers.Count > 10 || request.Headers.Sum(h => h.Key.Length + h.Value.Length) > 4096
			|| request.Headers.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Headers.Count)
			throw new CardOperationException("INVALID_PROVIDER_CALLBACK", "Invalid callback headers.", 401);
		var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);
		await notifications.ExecuteAsync(request.IntegrationId, headers, request.Payload.ToByteArray(), context.CancellationToken);
		return new MutationResponse { Success = true };
	});

	public override Task<GetCardsResponse> GetCards(GetCardsRequest request, ServerCallContext context) => Run(async () =>
	{
		var cards = await list.ExecuteAsync(Client(request.ClientId), request.Page == 0 ? 1 : request.Page,
			request.PageSize == 0 ? 20 : request.PageSize, context.CancellationToken);
		var response = new GetCardsResponse();
		response.Cards.AddRange(cards.Select(MapCard));
		return response;
	});

	public override Task<GetCardResponse> GetCard(GetCardRequest request, ServerCallContext context) => Run(async () =>
	{
		var card = await detail.ExecuteAsync(Client(request.ClientId), Id(request.CardId));
		return card is null ? new GetCardResponse { Found = false } : new GetCardResponse { Found = true, Card = MapCard(card) };
	});

	public override Task<MutationResponse> UpdateCard(UpdateCardRequest request, ServerCallContext context) => Run(async () =>
	{
		var error = await update.ExecuteAsync(new UpdateCreditCardCommand(Client(request.ClientId), Id(request.CardId),
			request.CardHolderName, request.IsDefault), context.CancellationToken);
		if (error is not null) throw new CardOperationException("CARD_NOT_FOUND", "Card not found.", 404);
		return new MutationResponse { Success = true };
	});

	public override Task<MutationResponse> DeleteCard(DeleteCardRequest request, ServerCallContext context) => Run(async () =>
	{
		await delete.ExecuteAsync(new DeleteCreditCardCommand(Client(request.ClientId), Id(request.CardId)), context.CancellationToken);
		return new MutationResponse { Success = true };
	});

	public override Task<CardRegistrationDto> StartCardRegistration(StartCardRegistrationRequest request, ServerCallContext context) =>
		Run(async () => Map(await start.ExecuteAsync(new(Client(request.ClientId), request.IdempotencyKey,
			request.CardHolderName, request.IsDefault, request.ConsentAccepted, request.ConsentVersion), context.CancellationToken)));

	public override Task<CardRegistrationDto> CompleteCardRegistration(CompleteCardRegistrationRequest request, ServerCallContext context) =>
		Run(async () => Map(await complete.ExecuteAsync(new(Client(request.ClientId), Id(request.SessionId),
			request.IdempotencyKey, request.TemporaryToken), context.CancellationToken)));

	public override Task<CardRegistrationDto> GetCardRegistration(CardRegistrationRequest request, ServerCallContext context) =>
		Run(async () => Map(await getRegistration.ExecuteAsync(Client(request.ClientId), Id(request.SessionId), ct: context.CancellationToken)));

	public override Task<CardRegistrationDto> CancelCardRegistration(CardRegistrationRequest request, ServerCallContext context) =>
		Run(async () => Map(await getRegistration.ExecuteAsync(Client(request.ClientId), Id(request.SessionId), cancel: true, ct: context.CancellationToken)));

	public override Task<CardRegistrationDto> ContinueCardRegistration(CardRegistrationRequest request, ServerCallContext context) =>
		Run(async () => Map(await continueRegistration.ExecuteAsync(Id(request.SessionId), Client(request.ClientId), context.CancellationToken)));

	private CreditCardDto MapCard(Domain.Entities.CreditCard card) => new()
	{
		Id = card.Id.ToString(), CardHolderName = card.CardHolderName, Last4Digits = card.Last4Digits,
		CardType = card.CardType.ToString(), ExpiryMonth = card.ExpiryMonth, ExpiryYear = card.ExpiryYear,
		IsDefault = card.IsDefault, CreatedAt = card.CreatedAt.ToString("O"), OperatorProvider = card.OperatorProvider,
		Available = card.IsAvailable(clock.UtcNow),
		Expired = card.ExpiryYear < clock.UtcNow.Year || (card.ExpiryYear == clock.UtcNow.Year && card.ExpiryMonth < clock.UtcNow.Month)
	};

	private static CardRegistrationDto Map(CardRegistrationView view) => new()
	{
		Id = view.Id.ToString(), State = view.State, ExpiresAt = view.ExpiresAt.ToString("O"),
		CardId = view.CardId?.ToString() ?? "", NextAction = view.NextAction ?? "", ErrorCode = view.ErrorCode ?? "",
		Provider = view.Initialization.Provider, MobileMethod = view.Initialization.Method,
		MobileVersion = view.Initialization.Version, PublicKey = view.Initialization.PublicKey ?? "", CleanupState = view.CleanupState
	};

	private static Guid Id(string id) => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty ? parsed
		: throw new CardOperationException("INVALID_IDENTIFIER", "Supply a valid identifier.");
	private static string Client(string clientId) => Id(clientId).ToString();

	private static async Task<T> Run<T>(Func<Task<T>> action)
	{
		try { return await action(); }
		catch (CardOperationException error)
		{
			var code = error.HttpStatus switch { 401 => StatusCode.Unauthenticated, 404 => StatusCode.NotFound, 409 => StatusCode.AlreadyExists, _ => StatusCode.InvalidArgument };
			throw new RpcException(new Status(code, error.Code));
		}
		catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
		{
			throw new RpcException(new Status(StatusCode.AlreadyExists, "CONCURRENT_CARD_CONFLICT"));
		}
		catch (DbUpdateConcurrencyException)
		{
			throw new RpcException(new Status(StatusCode.Aborted, "CONCURRENT_CARD_CHANGE"));
		}
		catch (InvalidOperationException)
		{
			throw new RpcException(new Status(StatusCode.Unavailable, "CARD_OPERATOR_UNAVAILABLE"));
		}
	}
}
