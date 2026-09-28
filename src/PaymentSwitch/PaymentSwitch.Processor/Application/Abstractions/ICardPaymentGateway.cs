using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public enum PaymentOperationOutcome
{
	Succeeded,
	DefinitiveFailure,
	RetryableFailure,
	Unknown
}

public sealed record PaymentOperationResult(
	PaymentOperationOutcome Outcome,
	string? ExternalReference,
	string? ErrorCode,
	string? ErrorMessage);

public interface ICardPaymentGateway
{
	Task<string> EnsureOperatorProviderAsync(
		CreditCard card,
		CancellationToken cancellationToken = default);

	Task<PaymentOperationResult> CheckReadinessAsync(
		CreditCard card,
		CancellationToken cancellationToken = default);

	Task<PaymentOperationResult> ChargeAsync(
		Payment payment,
		CreditCard card,
		CancellationToken cancellationToken = default);

	Task<PaymentOperationResult> ReconcileChargeAsync(
		Payment payment,
		CancellationToken cancellationToken = default);
}
