using ExternalConnections.PaymentOperators.PaymentOperators;
using ExternalConnections.PaymentOperators.Config;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Utilities.Money;

namespace PaymentSwitch.Processor.Infrastructure.Gateways;

public sealed class PaymentOperatorCardPaymentGateway(
	IPaymentOperatorResolver paymentOperatorResolver,
	ICurrencyCatalog currencyCatalog, PaymentOperatorRegistry registry) : ICardPaymentGateway
{
	public Task<bool> SupportsExecutionAsync(CreditCard card, string country, string currency, CancellationToken ct = default)
	{
		var integration = registry.GetRequired(card.OperatorProvider);
		return Task.FromResult(integration.SupportedCountries.Contains(country, StringComparer.OrdinalIgnoreCase)
			&& integration.SupportedCurrencies.Contains(currency, StringComparer.OrdinalIgnoreCase));
	}

	public async Task<string> EnsureOperatorProviderAsync(
		CreditCard card,
		CancellationToken cancellationToken = default)
	{
		if (!string.IsNullOrWhiteSpace(card.OperatorProvider))
		{
			if (!string.IsNullOrWhiteSpace(card.ProviderAccount))
			{
				var original = registry.GetRequired(card.OperatorProvider);
				if ((!string.IsNullOrEmpty(card.AdapterProvider) && original.Adapter != card.AdapterProvider) || original.Account != card.ProviderAccount || original.Environment != card.ProviderEnvironment || original.Version != card.IntegrationVersion)
					throw new InvalidOperationException("The original card integration identity must remain configured.");
			}
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
		await EnsureOperatorProviderAsync(card, cancellationToken);
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
