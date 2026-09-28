using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

public partial class MockPaymentOperatorAdapter(
	ILogger<MockPaymentOperatorAdapter> logger) : IPaymentOperatorAdapter
{
	public string ProviderCode => "Mock";

	public Task<PaymentOperatorCardResult> RegisterCardAsync(
		string cardNumber,
		string cvv,
		string cardHolderName,
		int expiryMonth,
		int expiryYear,
		CancellationToken cancellationToken = default)
	{
		var digits = DigitsOnlyRegex().Replace(cardNumber, string.Empty);
		var last4 = digits.Length >= 4 ? digits[^4..] : "4242";
		var brand = InferBrand(digits);
		var token = $"mock_card_{Guid.NewGuid():N}";
		logger.LogInformation(
			"Mock operator registered {Brand} card ending in {Last4}.",
			brand,
			last4);
		return Task.FromResult(new PaymentOperatorCardResult(
			true,
			token,
			last4,
			brand,
			expiryMonth,
			expiryYear,
			null));
	}

	public Task<CardReadinessResult> CheckCardAsync(
		string cardToken,
		CancellationToken cancellationToken = default)
	{
		var ready = !string.IsNullOrWhiteSpace(cardToken)
			&& !cardToken.Contains("declined", StringComparison.OrdinalIgnoreCase);
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

		if (cardToken.Contains("declined", StringComparison.OrdinalIgnoreCase))
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

	private static string InferBrand(string digits)
	{
		if (digits.StartsWith('4'))
		{
			return "Visa";
		}

		if (digits.Length >= 2
			&& int.TryParse(digits[..2], out var firstTwo)
			&& firstTwo is >= 51 and <= 55)
		{
			return "Mastercard";
		}

		return digits.StartsWith("34") || digits.StartsWith("37") ? "Amex" : "Other";
	}

	[GeneratedRegex("\\D")]
	private static partial Regex DigitsOnlyRegex();
}
