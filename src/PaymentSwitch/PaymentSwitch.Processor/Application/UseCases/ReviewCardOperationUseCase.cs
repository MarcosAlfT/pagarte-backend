using System.Security.Cryptography;
using System.Text.Json;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class ReviewCardOperationUseCase(ICardReviewRepository reviews, ICardRegistrationRepository registrations,
	ICardDetachmentRepository cards, ICardMutationScope mutations, ICreditCardRegistrationGateway gateway,
	VerifiedCardPersistenceService verifiedCards, IClock clock)
{
	public async Task<IReadOnlyList<CardReviewItem>> ListAsync(int limit, CancellationToken ct)
	{
		if (limit is < 1 or > 100) throw new CardOperationException("INVALID_REVIEW_LIMIT", "Supply a limit of 1 to 100.");
		return await reviews.ListAsync(limit, ct);
	}
	public async Task<CardReviewView> GetAsync(Guid id, CancellationToken ct) => View(await reviews.GetAsync(id, ct)
		?? throw new CardOperationException("REVIEW_NOT_FOUND", "Review not found.", 404));

	public async Task<CardReviewView> ExecuteAsync(ReviewCardCommand command, CancellationToken ct)
	{
		CardRegistrationRules.ValidateKey(command.Key);
		if (string.IsNullOrWhiteSpace(command.Actor) || command.Actor.Length > 300 || command.Actor.Any(char.IsControl)
			|| command.Kind is not ("Registration" or "SetupCleanup" or "CardRemoval")
			|| command.Action is not ("Reconcile" or "RetryRemoval") || (command.Kind == "Registration" && command.Action != "Reconcile")
			|| command.ReasonCode is not ("ProviderIncidentResolved" or "ProviderStatusAvailable" or "CustomerSupportReview"))
			throw new CardOperationException("INVALID_CARD_REVIEW", "Supply a supported review action and reason code.");
		var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
			command.Id, command.Kind, command.Action, command.ExpectedState, command.ReasonCode })));
		var previous = await reviews.FindAsync(command.Actor, command.Key, ct);
		if (previous is not null) return Replay(previous, fingerprint);
		var initialCard = command.Kind == "CardRemoval" ? await cards.GetAsync(command.Id, ct) : null;
		var initialSession = command.Kind != "CardRemoval" ? await registrations.GetAsync(command.Id, ct) : null;
		var owner = initialCard?.ClientId ?? initialSession?.ClientId ?? throw CardRegistrationRules.Missing();
		CardSetupContext? setup = null;
		CardOperatorIdentity? identity = null;
		string? reference = null;
		var operationId = Guid.NewGuid();
		await using (var tx = await mutations.BeginAsync(owner, ct))
		{
			previous = await reviews.FindAsync(command.Actor, command.Key, ct);
			if (previous is not null) return Replay(previous, fingerprint);
			var session = command.Kind != "CardRemoval" ? await registrations.GetAsync(command.Id, ct) : null;
			var card = command.Kind == "CardRemoval" ? await cards.GetAsync(command.Id, ct) : null;
			var state = State(command.Kind, session, card);
			if (state != command.ExpectedState || (command.Kind == "Registration" ? state != "Unresolved" : state != "RequiresReview")
				|| (command.Kind == "SetupCleanup" && session!.State is not (CardRegistrationState.Cancelled or CardRegistrationState.Expired)))
				throw new CardOperationException("CARD_REVIEW_STATE_CHANGED", "Reload the review state before acting.", 409);
			if ((card?.DetachmentLeaseUntil ?? session?.LeaseUntil) > clock.UtcNow)
				throw new CardOperationException("CARD_REVIEW_IN_PROGRESS", "Another provider operation is still leased.", 409);
			// An expired pending review is retained as unknown evidence, never silently marked successful.
			var expiredId = card?.DetachmentLeaseId ?? session?.LeaseId;
			if (expiredId is not null && await reviews.GetAsync(expiredId.Value, ct) is { CompletedAt: null } expired)
			{ expired.Outcome = "ResultUnknown"; expired.CompletedAt = clock.UtcNow; expired.AfterState = state; }
			if (card is not null)
			{
				if (!card.IsDeleted) throw new CardOperationException("CARD_REVIEW_STATE_CHANGED", "Only removed cards can be reviewed.", 409);
				identity = new(card.OperatorProvider, card.AdapterProvider, card.ProviderAccount, card.ProviderEnvironment, card.IntegrationVersion);
				if (command.Action == "RetryRemoval")
				{
					var capability = gateway.GetRemovalCapabilities(identity);
					if (!capability.Supported) throw new CardOperationException("REMOVAL_UNSUPPORTED", "The adapter cannot remove this card.", 409);
					if (await cards.HasDependenciesAsync(card.Id, capability.RefundsSurviveDetachment, ct))
						throw new CardOperationException("CARD_HAS_DEPENDENCIES", "Accepted work or shared resources still depend on this card.", 409);
				}
				reference = card.OperatorCardToken;
				card.DetachmentLeaseId = operationId; card.DetachmentLeaseUntil = clock.UtcNow.AddMinutes(2); card.DetachmentNextAttemptAt = null;
			}
			else
			{
				setup = CardRegistrationRules.Context(session!);
				session!.LeaseId = operationId; session.LeaseUntil = clock.UtcNow.AddMinutes(2); session.NextAttemptAt = null;
			}
			reviews.Add(new() { Id = operationId, TargetId = command.Id, Kind = command.Kind, Action = command.Action,
				Actor = command.Actor, RequestKey = command.Key, Fingerprint = fingerprint, ReasonCode = command.ReasonCode,
				BeforeState = state, AfterState = state, CreatedAt = clock.UtcNow });
			await tx.CommitAsync(ct);
		}

		CardSetupResult provider = new(CardSetupOutcome.Unknown);
		CardRemovalOutcome removal = CardRemovalOutcome.Unknown;
		try
		{
			if (command.Kind == "CardRemoval")
				removal = command.Action == "RetryRemoval" ? await gateway.RemoveCardAsync(identity!, reference!, $"detach-{command.Id:N}", ct)
					: await gateway.GetRemovalStatusAsync(identity!, reference!, ct);
			else
			{
				provider = await gateway.GetStatusAsync(setup!, ct);
				if (command.Kind == "SetupCleanup" && command.Action == "RetryRemoval" && provider.Outcome == CardSetupOutcome.Succeeded)
					removal = await gateway.RemoveSetupResourceAsync(setup!, ct);
			}
		}
		catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or TimeoutException or InvalidOperationException)
		{ /* Unknown configuration/provider outcomes retain the review barrier and the durable audit. */ }

		await using (var tx = await mutations.BeginAsync(owner, ct))
		{
			var audit = (await reviews.GetAsync(operationId, ct))!;
			var session = command.Kind != "CardRemoval" ? await registrations.GetAsync(command.Id, ct) : null;
			var card = command.Kind == "CardRemoval" ? await cards.GetAsync(command.Id, ct) : null;
			if ((card?.DetachmentLeaseId ?? session?.LeaseId) != operationId) audit.Outcome = "Superseded";
			else if (card is not null)
			{
				card.DetachmentLeaseId = null; card.DetachmentLeaseUntil = null;
				if (removal == CardRemovalOutcome.Removed) card.DetachmentState = "Removed";
				audit.Outcome = removal.ToString();
			}
			else
			{
				session!.LeaseId = null; session.LeaseUntil = null;
				if (command.Kind == "SetupCleanup")
				{
					if (provider.Outcome == CardSetupOutcome.NotFound) session.CleanupState = "NoResourceFound";
					else if (removal == CardRemovalOutcome.Removed) session.CleanupState = "Removed";
					else if (provider.Outcome == CardSetupOutcome.Succeeded && (command.Action == "Reconcile" || removal == CardRemovalOutcome.Unsupported)) session.CleanupState = "RetainedByProvider";
					audit.Outcome = session.CleanupState;
				}
				else if (session.IsTerminal || session.ExpiresAt <= clock.UtcNow)
				{
					if (!session.IsTerminal) session.Finish(CardRegistrationState.Expired, "SESSION_EXPIRED");
					audit.Outcome = "LateResultRetained";
				}
				else
				{
					if (provider.Outcome == CardSetupOutcome.Succeeded) await verifiedCards.ApplyAsync(session, provider.Card, ct);
					else if (provider.Outcome is CardSetupOutcome.NotFound or CardSetupOutcome.Declined) session.Finish(CardRegistrationState.Failed, "REVIEW_PROVIDER_REJECTED");
					else if (provider.Outcome == CardSetupOutcome.Cancelled) session.Finish(CardRegistrationState.Cancelled, "PROVIDER_CANCELLED");
					audit.Outcome = provider.Outcome.ToString();
				}
			}
			audit.AfterState = State(command.Kind, session, card); audit.CompletedAt = clock.UtcNow;
			await tx.CommitAsync(ct);
			return View(audit);
		}
	}

	private static string State(string kind, CardRegistrationSession? session, CreditCard? card) => kind switch {
		"CardRemoval" => card?.DetachmentState ?? "Missing", "SetupCleanup" => session?.CleanupState ?? "Missing", _ => session?.State.ToString() ?? "Missing" };
	private static CardReviewView Replay(CardReviewOperation operation, string fingerprint) => operation.Fingerprint == fingerprint ? View(operation)
		: throw new CardOperationException("IDEMPOTENCY_CONFLICT", "The review key was used with different inputs.", 409);
	private static CardReviewView View(CardReviewOperation value) => new(value.Id, value.TargetId, value.Kind, value.Action, value.Actor,
		value.ReasonCode, value.BeforeState, value.AfterState, value.Outcome, value.CreatedAt, value.CompletedAt);
}
