using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Utilities.Money;

namespace PaymentSwitch.Processor.Infrastructure.Gateways;

public sealed class PaymentOperatorCardPaymentGateway(
	IPaymentOperatorResolver paymentOperatorResolver,
	ICurrencyCatalog currencyCatalog) : ICardPaymentGateway
{
	public async Task<string> EnsureOperatorProviderAsync(
		CreditCard card,
		CancellationToken cancellationToken = default)
	{
		if (!string.IsNullOrWhiteSpace(card.OperatorProvider))
		{
			return card.OperatorProvider;
		}

		var paymentOperator = await paymentOperatorResolver.ResolveInternationalAsync();
		card.OperatorProvider = paymentOperator.ProviderCode;
		return paymentOperator.ProviderCode;
	}

	public async Task<PaymentOperationResult> CheckReadinessAsync(
		CreditCard card,
		CancellationToken cancellationToken = default)
	{
		var provider = await EnsureOperatorProviderAsync(card, cancellationToken);
		var result = await paymentOperatorResolver.GetAdapter(provider).CheckCardAsync(
			card.OperatorCardToken,
			cancellationToken);
		return Map(result.Outcome, null, result.ErrorCode, result.ErrorMessage);
	}

	public async Task<PaymentOperationResult> ChargeAsync(
		Payment payment,
		CreditCard card,
		CancellationToken cancellationToken = default)
	{
		var money = MoneyValue.FromMinorUnits(
			payment.TotalMinorUnits,
			payment.Currency,
			currencyCatalog);
		var result = await paymentOperatorResolver.GetAdapter(payment.OperatorProvider).ChargeAsync(
			card.OperatorCardToken,
			money.Amount,
			money.Currency,
			payment.CountryCode,
			payment.Reference,
			payment.CardChargeIdempotencyKey,
			cancellationToken);
		return Map(
			result.Outcome,
			result.OperatorPaymentId,
			result.ErrorCode,
			result.ErrorMessage);
	}

	public async Task<PaymentOperationResult> ReconcileChargeAsync(
		Payment payment,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(payment.OperatorPaymentId))
		{
			return new PaymentOperationResult(
				PaymentOperationOutcome.Unknown,
				null,
				"CHARGE_REFERENCE_UNAVAILABLE",
				"The charge has no operator reference; the idempotent charge request must be replayed.");
		}

		var result = await paymentOperatorResolver.GetAdapter(payment.OperatorProvider)
			.GetChargeStatusAsync(payment.OperatorPaymentId, cancellationToken);
		return Map(
			result.Outcome,
			result.OperatorPaymentId,
			result.ErrorCode,
			result.ErrorMessage);
	}

	private static PaymentOperationResult Map(
		PaymentOperatorOutcome outcome,
		string? reference,
		string? errorCode,
		string? errorMessage) =>
		new(
			outcome switch
			{
				PaymentOperatorOutcome.Succeeded => PaymentOperationOutcome.Succeeded,
				PaymentOperatorOutcome.DefinitiveDecline => PaymentOperationOutcome.DefinitiveFailure,
				PaymentOperatorOutcome.RetryableFailure => PaymentOperationOutcome.RetryableFailure,
				_ => PaymentOperationOutcome.Unknown
			},
			reference,
			errorCode,
			errorMessage);
}
