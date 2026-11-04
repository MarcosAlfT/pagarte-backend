using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Services;

public static class CardRegistrationRules
{
	public static void ValidateKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || key.Any(char.IsControl))
			throw new CardOperationException("INVALID_IDEMPOTENCY_KEY", "Supply an idempotency key of 1 to 100 printable characters.");
	}
	public static string Fingerprint(string name, bool isDefault, string consentVersion) => Convert.ToHexString(SHA256.HashData(
		Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { name, isDefault, consentVersion }))));
	public static CardOperatorIdentity Identity(CardRegistrationSession session) => new(session.IntegrationId,
		session.Provider, session.ProviderAccount, session.ProviderEnvironment, session.IntegrationVersion);
	public static CardSetupContext Context(CardRegistrationSession session) => new(session.Id, session.ClientId, Identity(session), session.ExpiresAt);
	public static CardRegistrationView View(CardRegistrationSession session, ICreditCardRegistrationGateway gateway) =>
		new(session.Id, session.State.ToString(), session.ExpiresAt, session.CardId, session.NextAction, session.ErrorCode,
			gateway.GetInitialization(Identity(session)), session.CleanupState);
	public static CardOperationException Missing() => new("REGISTRATION_NOT_FOUND", "Card registration not found.", 404);
}
