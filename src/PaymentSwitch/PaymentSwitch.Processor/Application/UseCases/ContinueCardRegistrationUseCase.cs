using System.Security.Cryptography;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class ContinueCardRegistrationUseCase(ICardRegistrationRepository registrations,
	ICreditCardRegistrationGateway gateway, IRegistrationTokenProtector tokens, ICardMutationScope mutations,
	VerifiedCardPersistenceService verifiedCards, IClock clock, ReconcileTerminatedCardRegistrationUseCase cleanup)
{
	public async Task<CardRegistrationView> ExecuteAsync(Guid id, string? clientId = null, CancellationToken ct = default)
	{
		var found = await registrations.GetAsync(id, ct);
		if (found is null || (clientId is not null && found.ClientId != clientId)) throw CardRegistrationRules.Missing();
		var owner = found.ClientId;
		if (found.IsTerminal) return await cleanup.ExecuteAsync(id, owner, ct);
		CardSetupContext context;
		string? token = null;
		Guid lease;
		await using (var transaction = await mutations.BeginAsync(owner, ct))
		{
			var session = (await registrations.GetAsync(id, ct))!;
			if (session.IsTerminal) return CardRegistrationRules.View(session, gateway);
			if (session.ExpiresAt <= clock.UtcNow)
			{
				session.Finish(CardRegistrationState.Expired, "SESSION_EXPIRED");
				await transaction.CommitAsync(ct);
				return CardRegistrationRules.View(session, gateway);
			}
			if (session.CompletionKey is null || session.LeaseUntil > clock.UtcNow
				|| session.State == CardRegistrationState.Unresolved)
				return CardRegistrationRules.View(session, gateway);
			if (session.NextAttemptAt > clock.UtcNow) return CardRegistrationRules.View(session, gateway);
			if (session.Attempts >= 5)
			{
				session.State = CardRegistrationState.Unresolved;
				session.ErrorCode = "SETUP_REQUIRES_REVIEW";
				session.ProtectedToken = null;
				session.NextAttemptAt = null;
				await transaction.CommitAsync(ct);
				return CardRegistrationRules.View(session, gateway);
			}
			if (session.ProtectedToken is not null && session.Attempts == 0)
			{
				if (session.TokenExpiresAt <= clock.UtcNow)
				{
					session.Finish(CardRegistrationState.Expired, "TOKEN_EXPIRED");
					await transaction.CommitAsync(ct);
					return CardRegistrationRules.View(session, gateway);
				}
				try { token = tokens.Unprotect(session.ProtectedToken); }
				catch (CryptographicException)
				{
					session.Finish(CardRegistrationState.Failed, "TOKEN_UNAVAILABLE");
					await transaction.CommitAsync(ct);
					return CardRegistrationRules.View(session, gateway);
				}
			}
			// Consume the encrypted temporary token before the one permitted save attempt.
			// After a crash, lookup the same operation; never blindly resend a consumed token.
			session.ProtectedToken = null;
			session.State = CardRegistrationState.Verifying;
			session.NextAction = null;
			session.LeaseId = lease = Guid.NewGuid();
			session.LeaseUntil = clock.UtcNow.AddMinutes(2);
			session.NextAttemptAt = session.LeaseUntil;
			session.Attempts++;
			context = CardRegistrationRules.Context(session);
			await transaction.CommitAsync(ct);
		}

		CardSetupResult result;
		try
		{
			result = token is null ? await gateway.GetStatusAsync(context, ct) : await gateway.SaveAsync(context, token, ct);
		}
		catch (Exception error) when (error is HttpRequestException or IOException or TaskCanceledException or TimeoutException)
		{
			result = new(CardSetupOutcome.Unknown);
		}
		finally { token = null; }

		await using (var transaction = await mutations.BeginAsync(owner, ct))
		{
			var session = (await registrations.GetAsync(id, ct))!;
			if (session.IsTerminal)
			{
				if (session.LeaseId == lease && session.CleanupState == "Pending")
				{
					session.LeaseId = null;
					session.LeaseUntil = null;
					session.NextAttemptAt = clock.UtcNow;
					await transaction.CommitAsync(ct);
				}
				return CardRegistrationRules.View(session, gateway);
			}
			if (session.LeaseId != lease) return CardRegistrationRules.View(session, gateway);
			session.LeaseId = null;
			session.LeaseUntil = null;
			if (session.ExpiresAt <= clock.UtcNow)
				session.Finish(CardRegistrationState.Expired, "SESSION_EXPIRED");
			else if (result.Outcome == CardSetupOutcome.Succeeded)
				await verifiedCards.ApplyAsync(session, result.Card, ct);
			else if (result.Outcome == CardSetupOutcome.Declined)
				session.Finish(CardRegistrationState.Failed, "SETUP_DECLINED");
			else if (result.Outcome == CardSetupOutcome.Cancelled)
				session.Finish(CardRegistrationState.Cancelled, "PROVIDER_CANCELLED");
			else if (result.Outcome == CardSetupOutcome.NotFound)
				session.Finish(CardRegistrationState.Failed, "SETUP_NOT_FOUND_RETOKENIZE");
			else
			{
				session.State = result.Outcome == CardSetupOutcome.RequiresAction ? CardRegistrationState.RequiresAction : CardRegistrationState.Verifying;
				// Only normalized action identifiers are allowed, never arbitrary provider HTML/URLs.
				session.NextAction = result.Outcome == CardSetupOutcome.RequiresAction && result.NextAction == "PresentChallenge" ? result.NextAction : null;
				session.ErrorCode = result.Outcome == CardSetupOutcome.Unknown ? "PROVIDER_PENDING" : null;
				session.NextAttemptAt = clock.UtcNow.AddSeconds(10);
			}
			await transaction.CommitAsync(ct);
			return CardRegistrationRules.View(session, gateway);
		}
	}

}
