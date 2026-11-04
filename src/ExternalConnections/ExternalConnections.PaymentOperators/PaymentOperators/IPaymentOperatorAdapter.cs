namespace ExternalConnections.PaymentOperators.PaymentOperators;

public enum PaymentOperatorOutcome
{
	Succeeded,
	DefinitiveDecline,
	RetryableFailure,
	Unknown
}

public interface IPaymentOperatorAdapter
{
	string ProviderCode { get; }

	Task<CardReadinessResult> CheckCardAsync(
		string cardToken,
		CancellationToken cancellationToken = default);

	Task<PaymentOperatorChargeResult> ChargeAsync(
		string cardToken,
		decimal amount,
		string currency,
		string countryCode,
		string reference,
		string idempotencyKey,
		CancellationToken cancellationToken = default);

	Task<PaymentOperatorChargeResult> GetChargeStatusAsync(
		string operatorPaymentId,
		CancellationToken cancellationToken = default);

	Task<PaymentOperatorRefundResult> RefundAsync(
		string operatorPaymentId,
		decimal amount,
		string currency,
		string reason,
		string? idempotencyKey = null,
		CancellationToken cancellationToken = default);
}

public sealed record CardReadinessResult(
	PaymentOperatorOutcome Outcome,
	string? ErrorCode,
	string? ErrorMessage)
{
	public bool Ready => Outcome == PaymentOperatorOutcome.Succeeded;
}

public sealed record PaymentOperatorChargeResult(
	PaymentOperatorOutcome Outcome,
	string? OperatorPaymentId,
	string? ErrorCode,
	string? ErrorMessage)
{
	public bool Success => Outcome == PaymentOperatorOutcome.Succeeded;
}

public sealed record PaymentOperatorRefundResult(
	PaymentOperatorOutcome Outcome,
	string? RefundId,
	string? ErrorCode,
	string? ErrorMessage)
{
	public bool Success => Outcome == PaymentOperatorOutcome.Succeeded;
}
