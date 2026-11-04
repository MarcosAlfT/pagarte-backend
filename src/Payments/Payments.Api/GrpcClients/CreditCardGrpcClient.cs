using Grpc.Core;
using PaymentSwitch.Contracts;

namespace Payments.Api.GrpcClients;

public sealed class CreditCardGrpcClient(CreditCardService.CreditCardServiceClient client, IConfiguration configuration)
{
	private Metadata Headers()
	{
		var key = configuration["WorkloadAuth:ApiKey"];
		if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("WorkloadAuth:ApiKey is not configured.");
		return new() { { "x-workload-api-key", key } };
	}
	public async Task<CardRemovalDto> GetRemovalAsync(string clientId, string id, CancellationToken ct) =>
		await client.GetCardRemovalAsync(new GetCardRequest { ClientId = clientId, CardId = id }, Headers(), cancellationToken: ct);
	public async Task<CardRegistrationPolicyDto> GetRegistrationPolicyAsync(CancellationToken ct) =>
		await client.GetCardRegistrationPolicyAsync(new CardRegistrationPolicyRequest(), Headers(), cancellationToken: ct);
	public async Task<MutationResponse> ReceiveNotificationAsync(string integrationId, IReadOnlyDictionary<string, string> headers, byte[] payload, CancellationToken ct)
	{
		var request = new CardSetupNotificationRequest { IntegrationId = integrationId, Payload = Google.Protobuf.ByteString.CopyFrom(payload) };
		request.Headers.Add(headers.ToDictionary(h => h.Key, h => h.Value));
		return await client.ReceiveCardSetupNotificationAsync(request, Headers(), cancellationToken: ct);
	}

	public async Task<GetCardsResponse> GetCardsAsync(string clientId, int page, int pageSize, CancellationToken ct) =>
		await client.GetCardsAsync(new GetCardsRequest { ClientId = clientId, Page = page, PageSize = pageSize }, Headers(), cancellationToken: ct);
	public async Task<GetCardResponse> GetCardAsync(string cardId, string clientId, CancellationToken ct) =>
		await client.GetCardAsync(new GetCardRequest { CardId = cardId, ClientId = clientId }, Headers(), cancellationToken: ct);
	public async Task<MutationResponse> UpdateCardAsync(string cardId, string clientId, string name, bool isDefault, CancellationToken ct) =>
		await client.UpdateCardAsync(new UpdateCardRequest { CardId = cardId, ClientId = clientId, CardHolderName = name, IsDefault = isDefault }, Headers(), cancellationToken: ct);
	public async Task<MutationResponse> DeleteCardAsync(string cardId, string clientId, CancellationToken ct) =>
		await client.DeleteCardAsync(new DeleteCardRequest { CardId = cardId, ClientId = clientId }, Headers(), cancellationToken: ct);
	public async Task<CardRegistrationDto> StartRegistrationAsync(string clientId, string key, string name, bool isDefault, bool consentAccepted, string consentVersion, CancellationToken ct) =>
		await client.StartCardRegistrationAsync(new StartCardRegistrationRequest { ClientId = clientId, IdempotencyKey = key,
			CardHolderName = name, IsDefault = isDefault, ConsentAccepted = consentAccepted, ConsentVersion = consentVersion }, Headers(), cancellationToken: ct);
	public async Task<CardRegistrationDto> CompleteRegistrationAsync(string clientId, string sessionId, string key, string token, CancellationToken ct) =>
		await client.CompleteCardRegistrationAsync(new CompleteCardRegistrationRequest { ClientId = clientId, SessionId = sessionId,
			IdempotencyKey = key, TemporaryToken = token }, Headers(), cancellationToken: ct);
	public async Task<CardRegistrationDto> GetRegistrationAsync(string clientId, string id, CancellationToken ct) =>
		await client.GetCardRegistrationAsync(new CardRegistrationRequest { ClientId = clientId, SessionId = id }, Headers(), cancellationToken: ct);
	public async Task<CardRegistrationDto> ContinueRegistrationAsync(string clientId, string id, CancellationToken ct) =>
		await client.ContinueCardRegistrationAsync(new CardRegistrationRequest { ClientId = clientId, SessionId = id }, Headers(), cancellationToken: ct);
	public async Task<CardRegistrationDto> CancelRegistrationAsync(string clientId, string id, CancellationToken ct) =>
		await client.CancelCardRegistrationAsync(new CardRegistrationRequest { ClientId = clientId, SessionId = id }, Headers(), cancellationToken: ct);
}
