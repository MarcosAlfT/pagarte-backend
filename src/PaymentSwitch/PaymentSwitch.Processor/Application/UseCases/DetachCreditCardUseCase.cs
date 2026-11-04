using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class DetachCreditCardUseCase(ICardDetachmentRepository cards, ICreditCardRegistrationGateway gateway,
	ICardMutationScope mutations, IClock clock)
{
	public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
	{
		var found = await cards.GetAsync(id, ct);
		if (found is null || !found.IsDeleted) return;
		var owner = found.ClientId;
		CardOperatorIdentity identity;
		string reference;
		Guid lease;
		await using (var transaction = await mutations.BeginAsync(owner, ct))
		{
			var card = (await cards.GetAsync(id, ct))!;
			if (!card.IsDeleted || card.DetachmentState is not ("Pending" or "Deferred")
				|| card.DetachmentLeaseUntil > clock.UtcNow || card.DetachmentNextAttemptAt > clock.UtcNow) return;
			identity = new(card.OperatorProvider, card.AdapterProvider, card.ProviderAccount, card.ProviderEnvironment, card.IntegrationVersion);
			var capabilities = gateway.GetRemovalCapabilities(identity);
			if (!capabilities.Supported)
			{
				card.DetachmentState = "Unsupported";
				card.DetachmentNextAttemptAt = null;
				await transaction.CommitAsync(ct);
				return;
			}
			if (await cards.HasDependenciesAsync(id, capabilities.RefundsSurviveDetachment, ct))
			{
				card.DetachmentState = "Deferred";
				card.DetachmentNextAttemptAt = clock.UtcNow.AddMinutes(1);
				await transaction.CommitAsync(ct);
				return;
			}
			if (card.DetachmentAttempts >= 5)
			{
				card.DetachmentState = "RequiresReview";
				card.DetachmentNextAttemptAt = null;
				card.DetachmentLeaseId = null;
				card.DetachmentLeaseUntil = null;
				await transaction.CommitAsync(ct);
				return;
			}
			card.DetachmentState = "Pending";
			card.DetachmentAttempts++;
			card.DetachmentLeaseId = lease = Guid.NewGuid();
			card.DetachmentLeaseUntil = clock.UtcNow.AddMinutes(2);
			card.DetachmentNextAttemptAt = card.DetachmentLeaseUntil;
			reference = card.OperatorCardToken;
			await transaction.CommitAsync(ct);
		}

		CardRemovalOutcome result;
		try { result = await gateway.RemoveCardAsync(identity, reference, $"detach-{id:N}", ct); }
		catch (Exception error) when (error is HttpRequestException or IOException or TaskCanceledException or TimeoutException)
		{ result = CardRemovalOutcome.Unknown; }

		await using (var transaction = await mutations.BeginAsync(owner, ct))
		{
			var card = (await cards.GetAsync(id, ct))!;
			if (card.DetachmentLeaseId != lease) return;
			card.DetachmentLeaseId = null;
			card.DetachmentLeaseUntil = null;
			card.DetachmentNextAttemptAt = null;
			card.DetachmentState = result switch
			{
				CardRemovalOutcome.Removed => "Removed",
				CardRemovalOutcome.Unsupported => "Unsupported",
				CardRemovalOutcome.Rejected => "RequiresReview",
				_ => card.DetachmentAttempts >= 5 ? "RequiresReview" : "Pending"
			};
			if (card.DetachmentState == "Pending") card.DetachmentNextAttemptAt = clock.UtcNow.AddSeconds(10);
			await transaction.CommitAsync(ct);
		}
	}
}
