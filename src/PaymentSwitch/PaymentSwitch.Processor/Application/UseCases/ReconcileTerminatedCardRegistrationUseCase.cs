using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;

namespace PaymentSwitch.Processor.Application.UseCases;

// A terminal local session never activates a late provider resource.
public sealed class ReconcileTerminatedCardRegistrationUseCase(ICardRegistrationRepository registrations,
	ICreditCardRegistrationGateway gateway, ICardMutationScope mutations, IClock clock)
{
	public async Task<CardRegistrationView> ExecuteAsync(Guid id, string owner, CancellationToken ct)
	{
		CardSetupContext context;
		Guid lease;
		await using (var transaction = await mutations.BeginAsync(owner, ct))
		{
			var session = (await registrations.GetAsync(id, ct))!;
			if (session.CleanupState != "Pending" || session.LeaseUntil > clock.UtcNow || session.NextAttemptAt > clock.UtcNow)
				return CardRegistrationRules.View(session, gateway);
			if (session.CleanupAttempts >= 5)
			{
				session.CleanupState = "RequiresReview";
				session.LeaseId = null;
				session.LeaseUntil = null;
				session.NextAttemptAt = null;
				await transaction.CommitAsync(ct);
				return CardRegistrationRules.View(session, gateway);
			}
			session.LeaseId = lease = Guid.NewGuid();
			session.LeaseUntil = clock.UtcNow.AddMinutes(2);
			session.NextAttemptAt = session.LeaseUntil;
			session.CleanupAttempts++;
			context = CardRegistrationRules.Context(session);
			await transaction.CommitAsync(ct);
		}
		CardSetupResult result;
		CardRemovalOutcome removal = CardRemovalOutcome.Unsupported;
		try
		{
			result = await gateway.GetStatusAsync(context, ct);
			if (result.Outcome == CardSetupOutcome.Succeeded)
				removal = await gateway.RemoveSetupResourceAsync(context, ct);
		}
		catch (Exception error) when (error is HttpRequestException or IOException or TaskCanceledException or TimeoutException)
		{ result = new(CardSetupOutcome.Unknown); }
		await using (var transaction = await mutations.BeginAsync(owner, ct))
		{
			var session = (await registrations.GetAsync(id, ct))!;
			if (session.LeaseId != lease || session.CleanupState != "Pending") return CardRegistrationRules.View(session, gateway);
			session.LeaseUntil = null;
			session.LeaseId = null;
			session.NextAttemptAt = null;
			if (result.Outcome == CardSetupOutcome.Unknown || removal is CardRemovalOutcome.Unknown or CardRemovalOutcome.Pending)
			{
				if (session.CleanupAttempts >= 5) session.CleanupState = "RequiresReview";
				else session.NextAttemptAt = clock.UtcNow.AddSeconds(10);
			}
			else session.CleanupState = result.Outcome == CardSetupOutcome.NotFound ? "NoResourceFound" : removal switch
			{
				CardRemovalOutcome.Removed => "Removed",
				CardRemovalOutcome.Rejected => "RequiresReview",
				_ => "RetainedByProvider"
			};
			await transaction.CommitAsync(ct);
			return CardRegistrationRules.View(session, gateway);
		}
	}
}
