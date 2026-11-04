using Microsoft.Extensions.Logging;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

public class MockPaymentOperatorAdapter(
	ILogger<MockPaymentOperatorAdapter> logger, MockCardSetupJournal setups, OperatorCardIdentity? identity = null) : IPaymentOperatorAdapter, ITokenizedCardSetupAdapter, IStoredCardRemovalAdapter
{
	public string ProviderCode => "Mock";

	public OperatorSetupNotification? VerifySetupNotification(string integrationId, IReadOnlyDictionary<string, string> headers, byte[] payload, DateTime utcNow) =>
		headers.TryGetValue("X-Operator-Timestamp", out var timestamp) && headers.TryGetValue("X-Operator-Signature", out var signature)
			? setups.VerifyNotification(integrationId, timestamp, signature, payload, utcNow) : null;
	public bool SupportsTokenizedSaving => true;
	public bool SupportsSetupRecovery => true;
	public bool SupportsDetachment => true;
	public Task<OperatorCardRemovalOutcome> GetRemovalStatusAsync(OperatorCardIdentity route, string reference, CancellationToken ct) =>
		Task.FromResult(setups.IsRemoved(route, reference) ? OperatorCardRemovalOutcome.Removed : OperatorCardRemovalOutcome.Pending);
	public OperatorCardRemovalCapabilities GetRemovalCapabilities(OperatorCardIdentity identity) => setups.GetRemovalCapabilities(identity);
	public Task<OperatorCardRemovalOutcome> RemoveCardAsync(OperatorCardIdentity identity, string reference, string operationKey, CancellationToken ct) => setups.RemoveCardAsync(identity, reference, operationKey, ct);
	public Task<OperatorCardRemovalOutcome> RemoveSetupResourceAsync(OperatorSetupContext context, CancellationToken ct) => setups.RemoveSetupResourceAsync(context, ct);
	public bool SupportsSetupChallenges => true;
	public string IntegrationVersion => "1";
	public string MobileMethod => "SyntheticToken";
	public string MobileVersion => "1";
	public Task<OperatorSetupResult> SaveTokenAsync(OperatorSetupContext context, string token, CancellationToken ct) => setups.SaveAsync(context, token, ct);
	public Task<OperatorSetupResult> GetSetupStatusAsync(OperatorSetupContext context, CancellationToken ct) => setups.GetStatusAsync(context, ct);

	public Task<CardReadinessResult> CheckCardAsync(
		string cardToken,
		CancellationToken cancellationToken = default)
	{
		var ready = !string.IsNullOrWhiteSpace(cardToken)
			&& !cardToken.Contains("declined", StringComparison.OrdinalIgnoreCase)
			&& (identity is null || !setups.IsRemoved(identity, cardToken));
		return Task.FromResult(ready
			? new CardReadinessResult(PaymentOperatorOutcome.Succeeded, null, null)
			: new CardReadinessResult(
				PaymentOperatorOutcome.DefinitiveDecline,
				"CARD_NOT_USABLE",
				"The stored card is not usable."));
	}

	public Task<PaymentOperatorChargeResult> ChargeAsync(
		string cardToken,
		decimal amount,
		string currency,
		string countryCode,
		string reference,
		string idempotencyKey,
		CancellationToken cancellationToken = default)
	{
		if (cardToken.Contains("retry", StringComparison.OrdinalIgnoreCase))
		{
			return Task.FromResult(new PaymentOperatorChargeResult(
				PaymentOperatorOutcome.RetryableFailure,
				null,
				"MOCK_RETRYABLE",
				"Mock retryable charge failure."));
		}

		if (cardToken.Contains("declined", StringComparison.OrdinalIgnoreCase) || (identity is not null && setups.IsRemoved(identity, cardToken)))
		{
			return Task.FromResult(new PaymentOperatorChargeResult(
				PaymentOperatorOutcome.DefinitiveDecline,
				null,
				"CARD_DECLINED",
				"Mock card decline."));
		}

		var paymentId = $"mock_payment_{idempotencyKey}";
		logger.LogInformation(
			"Mock operator charged {Amount} {Currency} for {Reference}.",
			amount,
			currency,
			reference);
		return Task.FromResult(new PaymentOperatorChargeResult(
			PaymentOperatorOutcome.Succeeded,
			paymentId,
			null,
			null));
	}

	public Task<PaymentOperatorChargeResult> GetChargeStatusAsync(
		string operatorPaymentId,
		CancellationToken cancellationToken = default) =>
		Task.FromResult(new PaymentOperatorChargeResult(
			PaymentOperatorOutcome.Succeeded,
			operatorPaymentId,
			null,
			null));

	public Task<PaymentOperatorRefundResult> RefundAsync(
		string operatorPaymentId,
		decimal amount,
		string currency,
		string reason,
		string? idempotencyKey = null,
		CancellationToken cancellationToken = default)
	{
		var refundId = $"mock_refund_{idempotencyKey ?? operatorPaymentId}";
		return Task.FromResult(new PaymentOperatorRefundResult(
			PaymentOperatorOutcome.Succeeded,
			refundId,
			null,
			null));
	}

}
