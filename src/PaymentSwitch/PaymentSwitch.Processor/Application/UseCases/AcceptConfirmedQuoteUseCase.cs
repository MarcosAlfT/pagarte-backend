using System.Text.Json;
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
	IClock clock,
	ICardMutationScope mutations,
    IQuoteSubmissionRepository submissions,
    IChargeFirstPaymentGateway? chargeFirstGateway = null)
{
	public async Task<PaymentExecutionResult> ExecuteAsync(
		ConfirmedQuoteExecution execution,
		CancellationToken cancellationToken = default,
        bool rejectUnavailableRoute = false)
	{
        if (string.IsNullOrWhiteSpace(execution.ClientId) || execution.ClientId.Length > 100 || execution.SourceQuoteId == Guid.Empty)
            return Failure("INVALID_EXECUTION_PACKAGE", "Execution ownership is invalid.");

		var fingerprint = ConfirmedQuoteValidator.Fingerprint(execution);
		Payment payment;
		await using (var transaction = await mutations.BeginAsync(execution.ClientId, cancellationToken))
		{
            var recorded = await submissions.GetAsync(execution.SourceQuoteId, cancellationToken);
            if (recorded is not null)
            {
                if (recorded.ClientId != execution.ClientId || recorded.SubmissionId != execution.SubmissionId ||
                    recorded.PackageFingerprint != fingerprint)
                    return Failure("SOURCE_QUOTE_FINGERPRINT_CONFLICT", "The quote already has another submission.");
                if (recorded.PaymentId is { } acceptedId)
                {
                    var accepted = await paymentRepository.GetByIdAsync(acceptedId, cancellationToken)
                        ?? throw new InvalidOperationException("Accepted submission payment is missing.");
                    if (accepted.NeedsBillVerification && execution.BillVerification is { } evidence)
                    {
                        var now = clock.UtcNow;
                        var billError = BillVerificationPolicy.Validate(execution, now);
                        if (billError is null && accepted.ChargeFirst && evidence.BillingPeriod != accepted.BillingPeriod)
                            billError = ("BILL_CHANGED", "The billing period changed. Consult again.");
                        if (billError is null)
                            accepted.SetBillVerification(execution.BillKey!, JsonSerializer.Serialize(evidence), evidence.ExpiresAt!.Value, now);
                        else if (billError.Value.Code != Payment.StaleBillVerification)
                            accepted.MarkFailed(now, billError.Value.Code, billError.Value.Message);
                        if (billError?.Code != Payment.StaleBillVerification)
                            await transaction.CommitAsync(cancellationToken);
                    }
                    return PaymentExecutionResultMapper.Map(accepted, replayed: true) with { SubmissionRecorded = true };
                }
                return Failure(recorded.ErrorCode!, recorded.ErrorMessage!) with { Replayed = true, SubmissionRecorded = true };
            }

            async Task<PaymentExecutionResult> RejectAsync(string code, string message)
            {
                if (execution.SubmissionId is { } submissionId && submissionId != Guid.Empty)
                {
                    submissions.Add(new QuoteSubmissionOutcome
                    {
                        SourceQuoteId = execution.SourceQuoteId, SubmissionId = submissionId,
                        ClientId = execution.ClientId, PackageFingerprint = fingerprint,
                        ErrorCode = code, ErrorMessage = message, RecordedAt = clock.UtcNow
                    });
                    await transaction.CommitAsync(cancellationToken);
                    return Failure(code, message) with { SubmissionRecorded = true };
                }
                return Failure(code, message);
            }
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

            // The legacy RPC may recover an accepted payment, but cannot authorize a new one.
            if (execution.SubmissionId is null)
                return Failure("SUBMISSION_REQUIRED", "New execution requires a durably claimed quote submission.");
            var validationError = validator.Validate(execution, clock.UtcNow);
            if (validationError is not null)
                return await RejectAsync("INVALID_EXECUTION_PACKAGE", validationError);
            if (rejectUnavailableRoute)
                return execution.RequiresBillVerification
                    ? Failure("INVALID_EXECUTION_PACKAGE", "Bill-backed quotes require bill verification.")
                    : await RejectAsync("QUOTE_ROUTE_UNAVAILABLE", "The original payment route is unavailable. Consult again.");
            if (BillVerificationPolicy.Validate(execution, clock.UtcNow) is { } initialBillError)
                return initialBillError.Code == Payment.StaleBillVerification
                    ? Failure(initialBillError.Code, initialBillError.Message) with { Final = false }
                    : await RejectAsync(initialBillError.Code, initialBillError.Message);
			var card = await creditCardRepository.GetByIdAsync(execution.CreditCardId);
			if (card is null || card.ClientId != execution.ClientId)
			{
				return await RejectAsync(
					"CARD_NOT_OWNED",
					"The stored card does not belong to the authenticated client.");
			}

			if (!card.IsAvailable(clock.UtcNow))
				return await RejectAsync("CARD_UNAVAILABLE", "The stored card is unavailable for new payments.");

			if (!await cardPaymentGateway.SupportsExecutionAsync(card, execution.CountryCode, execution.Currency, cancellationToken))
				return await RejectAsync("CARD_MARKET_UNSUPPORTED", "The card integration does not support this country and currency.");

			var operatorProvider = await cardPaymentGateway.EnsureOperatorProviderAsync(
				card,
				cancellationToken);
			var executionOptions = options.Value;
			var currency = currencyCatalog.GetRequired(execution.Currency);
			var notificationEndpoint = chargeFirstGateway?.ResolveNotificationEndpoint(execution, operatorProvider);
			if (notificationEndpoint is not null && !BillVerificationPolicy.MatchesDevelopmentBillIdentity(execution))
				return await RejectAsync("BILL_IDENTITY_REQUIRED", "The quote has no supported billing period or account. Consult again.");
			if (execution.BillKey is { } billKey)
			{
				var reservation = await paymentRepository.CheckBillReservationAsync(
					billKey, execution.BillVerification!.CheckedAt!.Value, cancellationToken);
				if (reservation == BillReservationStatus.Reserved)
					return await RejectAsync("BILL_PAYMENT_BLOCKED", "A payment cannot be started for this bill.");
				if (reservation == BillReservationStatus.VerificationPredatesRelease)
					return Failure(Payment.StaleBillVerification, BillVerificationPolicy.RefreshMessage) with { Final = false };
				if (BillVerificationPolicy.Validate(execution, clock.UtcNow) is { } billError)
					return Failure(billError.Code, billError.Message) with { Final = false };
			}

			payment = Payment.Create(
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

			if (execution.BillKey is { } key && execution.BillVerification is { } verification)
				payment.SetBillVerification(key, JsonSerializer.Serialize(verification), verification.ExpiresAt!.Value, clock.UtcNow);
			if (notificationEndpoint is not null)
				payment.PinChargeFirst(notificationEndpoint, execution.BillVerification!.BillingPeriod!, executionOptions.MaxCompensationAttempts);

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

            if (execution.SubmissionId is { } submissionId)
                submissions.Add(new QuoteSubmissionOutcome
                {
                    SourceQuoteId = execution.SourceQuoteId, SubmissionId = submissionId,
                    ClientId = execution.ClientId, PackageFingerprint = fingerprint,
                    PaymentId = payment.Id, RecordedAt = clock.UtcNow
                });
			await transaction.CommitAsync(cancellationToken);
		}

		await continuePaymentExecutionUseCase.ExecuteAsync(payment.Id, cancellationToken);

		var current = await paymentRepository.GetByIdAsync(payment.Id, cancellationToken)
			?? payment;
		return PaymentExecutionResultMapper.Map(current) with { SubmissionRecorded = execution.SubmissionId is not null };
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
