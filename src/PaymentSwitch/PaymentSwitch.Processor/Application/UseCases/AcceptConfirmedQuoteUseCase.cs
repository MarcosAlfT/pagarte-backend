using Microsoft.Extensions.Options;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;
using Utilities.Money;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class AcceptConfirmedQuoteUseCase(
	IPaymentRepository paymentRepository,
	ICreditCardRepository creditCardRepository,
	ICardPaymentGateway cardPaymentGateway,
	ConfirmedQuoteValidator validator,
	ContinuePaymentExecutionUseCase continuePaymentExecutionUseCase,
	ICurrencyCatalog currencyCatalog,
	IOptions<PaymentExecutionOptions> options,
	IClock clock)
{
	public async Task<PaymentExecutionResult> ExecuteAsync(
		ConfirmedQuoteExecution execution,
		CancellationToken cancellationToken = default)
	{
		var validationError = validator.Validate(execution, clock.UtcNow);
		if (validationError is not null)
		{
			return Failure("INVALID_EXECUTION_PACKAGE", validationError);
		}

		var fingerprint = ConfirmedQuoteValidator.Fingerprint(execution);
		var existing = await paymentRepository.GetBySourceQuoteIdAsync(
			execution.SourceQuoteId,
			cancellationToken);
		if (existing is not null)
		{
			if (existing.ClientId != execution.ClientId)
			{
				return Failure("SOURCE_QUOTE_CONFLICT", "The source quote is already owned by another client.");
			}

			if (!string.Equals(
				existing.PackageFingerprint,
				fingerprint,
				StringComparison.Ordinal))
			{
				return Failure(
					"SOURCE_QUOTE_FINGERPRINT_CONFLICT",
					"The source quote was already submitted with a different execution package or card.");
			}

			return PaymentExecutionResultMapper.Map(existing, replayed: true);
		}

		var card = await creditCardRepository.GetByIdAsync(execution.CreditCardId);
		if (card is null || card.ClientId != execution.ClientId)
		{
			return Failure(
				"CARD_NOT_OWNED",
				"The stored card does not belong to the authenticated client.");
		}

		var operatorProvider = await cardPaymentGateway.EnsureOperatorProviderAsync(
			card,
			cancellationToken);
		var executionOptions = options.Value;
		var currency = currencyCatalog.GetRequired(execution.Currency);
		var payment = Payment.Create(
			new PaymentCreationData(
				execution.ClientId,
				execution.SourceQuoteId,
				fingerprint,
				execution.CreditCardId,
				execution.ServiceId,
				execution.ServiceName,
				execution.CountryId,
				execution.CountryCode,
				execution.RouteId,
				execution.RouteType,
				execution.PaymentNetworkId,
				execution.ProviderId,
				execution.ExternalSourceCode,
				execution.ExternalServiceCode,
				execution.CompanyPaymentEndpoint,
				execution.CompanyRollbackEndpoint,
				execution.CompanyStatusEndpoint,
				execution.CompanyCredentialReference,
				execution.TotalMinorUnits,
				currency.Code,
				currency.MinorUnitScale,
				execution.RoundingMode,
				execution.QuoteCreatedAt,
				execution.QuoteExpiresAt,
				operatorProvider,
				executionOptions.MaxCardCheckAttempts,
				executionOptions.MaxCompanyPaymentAttempts,
				executionOptions.MaxChargeAttempts,
				executionOptions.MaxRollbackAttempts,
				executionOptions.RetryIntervalSeconds),
			clock.UtcNow);

		foreach (var component in execution.Components)
		{
			payment.Details.Add(new PaymentDetail
			{
				Id = Guid.NewGuid(),
				PaymentId = payment.Id,
				ComponentType = component.ComponentType,
				Description = component.Description,
				Source = component.Source,
				IsVisibleToClient = component.VisibleToClient,
				DisplayOrder = component.DisplayOrder,
				MinorUnits = component.MinorUnits,
				Currency = component.Currency
			});
		}

		foreach (var reference in execution.ReferenceValues)
		{
			payment.ReferenceValues.Add(new PaymentReferenceValue
			{
				Id = Guid.NewGuid(),
				PaymentId = payment.Id,
				Key = reference.Key,
				Value = reference.Value
			});
		}

		var insertion = await paymentRepository.InsertIfSourceQuoteAbsentAsync(
			payment,
			cancellationToken);
		if (!insertion.Inserted)
		{
			if (insertion.Payment.ClientId != execution.ClientId
				|| !string.Equals(
					insertion.Payment.PackageFingerprint,
					fingerprint,
					StringComparison.Ordinal))
			{
				return Failure(
					"SOURCE_QUOTE_FINGERPRINT_CONFLICT",
					"The source quote was concurrently submitted with a different execution package or card.");
			}

			return PaymentExecutionResultMapper.Map(insertion.Payment, replayed: true);
		}

		await continuePaymentExecutionUseCase.ExecuteAsync(payment.Id, cancellationToken);

		var current = await paymentRepository.GetByIdAsync(payment.Id, cancellationToken)
			?? payment;
		return PaymentExecutionResultMapper.Map(current);
	}

	private static PaymentExecutionResult Failure(string code, string message) =>
		new(
			false,
			null,
			null,
			PaymentExecutionResultStatus.Failed,
			code,
			message,
			false,
			true);
}
