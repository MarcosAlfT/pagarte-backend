using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.Services;

// Called only inside the owner mutation transaction, after an authoritative provider lookup.
public sealed class VerifiedCardPersistenceService(ICreditCardRepository cards, IUnitOfWork unitOfWork,
	IRegistrationTokenProtector tokens, IClock clock)
{
	public async Task ApplyAsync(CardRegistrationSession session, VerifiedCardEvidence? evidence, CancellationToken ct)
	{
		if (evidence is null || string.IsNullOrWhiteSpace(evidence.Reference) || evidence.Reference.Length > 500
			|| string.IsNullOrEmpty(evidence.Last4) || evidence.Last4.Length != 4 || evidence.Last4.Any(c => c is < '0' or > '9')
			|| !Enum.TryParse<CardType>(evidence.CardType, true, out var type) || !Enum.IsDefined(type)
			|| evidence.ExpiryMonth is < 1 or > 12 || evidence.ExpiryYear is < 2000 or > 9998
			|| new DateTime(evidence.ExpiryYear, evidence.ExpiryMonth, 1).AddMonths(1) <= clock.UtcNow)
		{
			session.Finish(CardRegistrationState.Failed, "INVALID_CARD_EVIDENCE");
			return;
		}
		var identity = CardRegistrationRules.Identity(session);
		var referenceDigest = tokens.Digest($"{identity.Provider}|{identity.Account}|{identity.Environment}|saved-card", evidence.Reference);
		var card = await cards.FindProviderReferenceAsync(session.ClientId, session.IntegrationId, referenceDigest, ct);
		if (card is null)
		{
			card = CreditCard.Create(session.ClientId, session.IntegrationId, evidence.Reference,
				session.CardHolderName, evidence.Last4, type, evidence.ExpiryMonth, evidence.ExpiryYear, false, clock.UtcNow);
			card.AdapterProvider = identity.Provider;
			card.ProviderAccount = identity.Account;
			card.ProviderEnvironment = identity.Environment;
			card.IntegrationVersion = identity.Version;
			card.ProviderReferenceDigest = referenceDigest;
			await cards.CreateAsync(card);
		}
		else
		{
			// Refresh only attributes supplied by fresh provider evidence when deduplicating.
			card.Last4Digits = evidence.Last4;
			card.CardType = type;
			card.ExpiryMonth = evidence.ExpiryMonth;
			card.ExpiryYear = evidence.ExpiryYear;
			card.SetupVerified = true;
		}
		if (session.MakeDefault)
		{
			await cards.ClearDefaultsAsync(session.ClientId, ct);
			await unitOfWork.SaveChangesAsync(ct);
			card.Update(card.CardHolderName, true, clock.UtcNow);
		}
		session.CardId = card.Id;
		session.Finish(CardRegistrationState.Succeeded);
	}
}
