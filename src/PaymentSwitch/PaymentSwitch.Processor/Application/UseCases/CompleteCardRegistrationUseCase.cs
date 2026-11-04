using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class CompleteCardRegistrationUseCase(ICardRegistrationRepository registrations,
	ICreditCardRegistrationGateway gateway, IRegistrationTokenProtector tokens, ICardMutationScope mutations,
	ContinueCardRegistrationUseCase continuation, IClock clock)
{
	public async Task<CardRegistrationView> ExecuteAsync(CompleteCardRegistrationCommand command, CancellationToken ct = default)
	{
		CardRegistrationRules.ValidateKey(command.IdempotencyKey);
		if (string.IsNullOrWhiteSpace(command.Token) || command.Token.Length > 2048 || command.Token.Any(c => c is < '!' or > '~'))
			throw new CardOperationException("INVALID_TOKEN", "Supply a temporary token.");
		await using (var transaction = await mutations.BeginAsync(command.ClientId, ct))
		{
			var session = await registrations.GetAsync(command.SessionId, ct);
			if (session is null || session.ClientId != command.ClientId) throw CardRegistrationRules.Missing();
			var identity = CardRegistrationRules.Identity(session);
			var digest = tokens.Digest($"{identity.Provider}|{identity.Account}|{identity.Environment}", command.Token);
			var keyOwner = await registrations.FindCompletionAsync(command.ClientId, command.IdempotencyKey, ct);
			if (keyOwner is not null && keyOwner.Id != session.Id)
				throw new CardOperationException("IDEMPOTENCY_CONFLICT", "The completion key belongs to another registration.", 409);
			if (session.CompletionKey is not null && (session.CompletionKey != command.IdempotencyKey || session.TokenDigest != digest))
				throw new CardOperationException("IDEMPOTENCY_CONFLICT", "The registration was completed with different inputs.", 409);
			if (session.IsTerminal) return CardRegistrationRules.View(session, gateway);
			if (session.ExpiresAt <= clock.UtcNow)
			{
				session.Finish(CardRegistrationState.Expired, "SESSION_EXPIRED");
				await transaction.CommitAsync(ct);
				return CardRegistrationRules.View(session, gateway);
			}
			if (session.CompletionKey is null)
			{
				var claimed = await registrations.FindTokenAsync(digest, ct);
				if (claimed is not null && claimed.Id != session.Id)
					throw new CardOperationException("TOKEN_ALREADY_CLAIMED", "The token was already used for a registration.", 409);
				session.CompletionKey = command.IdempotencyKey;
				session.TokenDigest = digest;
				session.ProtectedToken = tokens.Protect(command.Token);
				session.TokenExpiresAt = clock.UtcNow.AddMinutes(10);
				session.NextAttemptAt = clock.UtcNow;
			}
			await transaction.CommitAsync(ct);
		}
		return await continuation.ExecuteAsync(command.SessionId, command.ClientId, ct);
	}
}
