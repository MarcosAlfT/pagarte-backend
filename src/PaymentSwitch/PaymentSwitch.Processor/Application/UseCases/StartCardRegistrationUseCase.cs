using PaymentSwitch.Messaging;
using Microsoft.Extensions.Options;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class StartCardRegistrationUseCase(ICardRegistrationRepository registrations,
	ICreditCardRegistrationGateway gateway, ICardMutationScope mutations, IClock clock, IRegistrationTokenProtector tokens, IOptions<CardRegistrationOptions> options, ICardDetachmentRepository cards)
{
	public async Task<CardRegistrationView> ExecuteAsync(StartCardRegistrationCommand command, CancellationToken ct = default)
	{
		CardRegistrationRules.ValidateKey(command.IdempotencyKey);
		if (string.IsNullOrWhiteSpace(command.CardHolderName) || command.CardHolderName.Length > 200)
			throw new CardOperationException("INVALID_DISPLAY_NAME", "Supply a display name of 1 to 200 characters.");
		if (!command.ConsentAccepted || string.IsNullOrWhiteSpace(command.ConsentVersion) || command.ConsentVersion.Length > 100)
			throw new CardOperationException("CARD_CONSENT_REQUIRED", "Accept the current saved-card policy before starting registration.");
		var fingerprint = CardRegistrationRules.Fingerprint(command.CardHolderName, command.IsDefault, command.ConsentVersion);
		await using var transaction = await mutations.BeginAsync(command.ClientId, ct);
		var existing = await registrations.FindCreationAsync(command.ClientId, command.IdempotencyKey, ct);
		if (existing is not null)
		{
			if (existing.CreationFingerprint != fingerprint)
				throw new CardOperationException("IDEMPOTENCY_CONFLICT", "The key was used with different registration inputs.", 409);
			if (!existing.IsTerminal && existing.ExpiresAt <= clock.UtcNow)
			{
				existing.Finish(CardRegistrationState.Expired, "SESSION_EXPIRED");
				await transaction.CommitAsync(ct);
			}
			return CardRegistrationRules.View(existing, gateway);
		}
		if (command.ConsentVersion != options.Value.ConsentVersion)
			throw new CardOperationException("CONSENT_VERSION_CHANGED", "Reload the current saved-card policy.", 409);
		if (await cards.HasRegistrationBarrierAsync(command.ClientId, ct))
			throw new CardOperationException("CARD_REMOVAL_IN_PROGRESS", "Wait for provider card removal to be resolved before starting registration.", 409);
		tokens.EnsureConfigured();
		var selected = gateway.SelectOperator();
		var session = new CardRegistrationSession
		{
			Id = Guid.NewGuid(), ClientId = command.ClientId, CreationKey = command.IdempotencyKey,
			CreationFingerprint = fingerprint, CardHolderName = command.CardHolderName, MakeDefault = command.IsDefault,
			IntegrationId = selected.IntegrationId, Provider = selected.Provider, ProviderAccount = selected.Account,
			ProviderEnvironment = selected.Environment, IntegrationVersion = selected.Version,
			ConsentVersion = command.ConsentVersion, ConsentAcceptedAt = clock.UtcNow,
			State = CardRegistrationState.Pending, CreatedAt = clock.UtcNow, ExpiresAt = clock.UtcNow.AddMinutes(15),
			NextAttemptAt = clock.UtcNow.AddMinutes(15)
		};
		registrations.Add(session);
		await transaction.CommitAsync(ct);
		return CardRegistrationRules.View(session, gateway);
	}
}
