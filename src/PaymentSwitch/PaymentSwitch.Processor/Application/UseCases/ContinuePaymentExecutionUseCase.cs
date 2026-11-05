using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class ContinuePaymentExecutionUseCase(
	IPaymentRepository paymentRepository,
	ICardPaymentGateway cardPaymentGateway,
	ICompanyPaymentGateway companyPaymentGateway,
	IUnitOfWork unitOfWork,
	IClock clock,
	IChargeFirstPaymentGateway? chargeFirstGateway = null)
{
	public async Task ExecuteAsync(
		Guid paymentId,
		CancellationToken cancellationToken = default)
	{
		var payment = await paymentRepository.GetByIdAsync(paymentId, cancellationToken);
		if (payment is null || payment.IsFinal)
		{
			return;
		}

		for (var transition = 0; transition < 8 && !payment.IsFinal; transition++)
		{
			if (payment.NextAttemptAt is not null && payment.NextAttemptAt > clock.UtcNow)
			{
				return;
			}
			if (payment.ChargeFirst && payment.NextAttemptAt is null) return;
			if (payment.Status == PaymentTransactionStatus.CardReady && payment.BillKey is not null &&
				(payment.BillVerificationExpiresAt is not { } expiresAt || expiresAt <= clock.UtcNow))
			{
				if (!payment.NeedsBillVerification)
				{
					payment.RequestBillVerification(clock.UtcNow);
					await SaveAsync(payment, cancellationToken);
				}
				return;
			}

			var continueImmediately = payment.Status switch
			{
				PaymentTransactionStatus.Accepted
					or PaymentTransactionStatus.CardCheckRetryScheduled
					or PaymentTransactionStatus.CheckingCard =>
					await CheckCardAsync(payment, cancellationToken),
				PaymentTransactionStatus.CardReady when payment.ChargeFirst =>
					await ChargeCardAsync(payment, cancellationToken),
				PaymentTransactionStatus.CardReady
					or PaymentTransactionStatus.CompanyPaymentRetryScheduled
					or PaymentTransactionStatus.CompanyPaymentOutcomeUnknown
					or PaymentTransactionStatus.PayingCompany =>
					await PayCompanyAsync(payment, cancellationToken),
				PaymentTransactionStatus.CompanyPaid
					or PaymentTransactionStatus.ChargeRetryScheduled
					or PaymentTransactionStatus.ChargeOutcomeUnknown
					or PaymentTransactionStatus.ChargingCard =>
					await ChargeCardAsync(payment, cancellationToken),
				PaymentTransactionStatus.RollingBackCompany
					or PaymentTransactionStatus.RollbackRetryScheduled =>
					await RollbackCompanyAsync(payment, cancellationToken),
				PaymentTransactionStatus.ChargedAwaitingCompany or PaymentTransactionStatus.NotifyingCompany =>
					await NotifyCompanyAsync(payment, cancellationToken),
				PaymentTransactionStatus.CompensationPending or PaymentTransactionStatus.RefundingCompensation =>
					await CompensateAsync(payment, cancellationToken),
				_ => false
			};

			if (!continueImmediately)
			{
				return;
			}
		}
	}

	private async Task<bool> CheckCardAsync(
		Payment payment,
		CancellationToken cancellationToken)
	{
		if (payment.Status == PaymentTransactionStatus.CheckingCard)
		{
			payment.ScheduleCardCheckRetry(
				clock.UtcNow,
				"CARD_CHECK_INTERRUPTED",
				"A previous card check did not record an outcome.");
		}

		payment.BeginCardCheck(clock.UtcNow);
		await SaveAsync(payment, cancellationToken);
		var result = await cardPaymentGateway.CheckReadinessAsync(
			payment.CreditCard,
			cancellationToken);

		if (result.Outcome == PaymentOperationOutcome.Succeeded)
		{
			payment.MarkCardReady(clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			return true;
		}

		if (result.Outcome == PaymentOperationOutcome.DefinitiveFailure
			|| payment.CardCheckAttempts >= payment.MaxCardCheckAttempts)
		{
			payment.MarkFailed(
				clock.UtcNow,
				result.ErrorCode ?? "CARD_NOT_USABLE",
				result.ErrorMessage ?? "The stored card is not usable.");
		}
		else
		{
			payment.ScheduleCardCheckRetry(
				clock.UtcNow,
				result.ErrorCode,
				result.ErrorMessage);
		}

		await SaveAsync(payment, cancellationToken);
		return false;
	}

	private async Task<bool> PayCompanyAsync(
		Payment payment,
		CancellationToken cancellationToken)
	{
		var firstCompanyAttempt = payment.Status == PaymentTransactionStatus.CardReady && payment.CompanyPaymentAttempts == 0;
		PaymentOperationResult result;
		if (payment.Status == PaymentTransactionStatus.CompanyPaymentOutcomeUnknown
			&& !string.IsNullOrWhiteSpace(payment.CompanyReference))
		{
			payment.BeginCompanyPaymentReconciliation(clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			result = await companyPaymentGateway.ReconcilePaymentAsync(payment, cancellationToken);
		}
		else
		{
			if (payment.Status == PaymentTransactionStatus.PayingCompany)
			{
				payment.MarkCompanyPaymentUnknown(
					clock.UtcNow,
					payment.CompanyReference,
					"COMPANY_PAYMENT_INTERRUPTED",
					"A previous company payment did not record an outcome.");
			}

			payment.BeginCompanyPayment(clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			result = await companyPaymentGateway.PayServiceAsync(payment, cancellationToken);
		}

		if (result.Outcome == PaymentOperationOutcome.Succeeded
			&& !string.IsNullOrWhiteSpace(result.ExternalReference))
		{
			payment.MarkCompanyPaid(result.ExternalReference, clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			return true;
		}

		if (result.Outcome == PaymentOperationOutcome.Unknown
			|| result.Outcome == PaymentOperationOutcome.Succeeded)
		{
			if (payment.CompanyPaymentAttempts >= payment.MaxCompanyPaymentAttempts)
			{
				payment.MarkManualIntervention(
					clock.UtcNow,
					result.ErrorCode ?? "COMPANY_PAYMENT_UNKNOWN",
					result.ErrorMessage ?? "The company payment outcome could not be confirmed.");
			}
			else
			{
				payment.MarkCompanyPaymentUnknown(
					clock.UtcNow,
					result.ExternalReference,
					result.ErrorCode ?? "COMPANY_PAYMENT_UNKNOWN",
					result.ErrorMessage ?? "The company payment outcome is unknown.");
			}
		}
		else if (firstCompanyAttempt && result.Outcome == PaymentOperationOutcome.DefinitiveFailure)
		{
			payment.MarkFirstCompanyPaymentRejected(
				clock.UtcNow,
				result.ErrorCode ?? "COMPANY_PAYMENT_FAILED",
				result.ErrorMessage ?? "The company payment failed.");
		}
		else if (result.Outcome == PaymentOperationOutcome.DefinitiveFailure
			|| payment.CompanyPaymentAttempts >= payment.MaxCompanyPaymentAttempts)
		{
			if (payment.BillKey is not null)
				payment.MarkManualIntervention(clock.UtcNow, "COMPANY_PAYMENT_UNKNOWN",
					"The company payment outcome must be confirmed before releasing this bill.");
			else
				payment.MarkFailed(
					clock.UtcNow,
					result.ErrorCode ?? "COMPANY_PAYMENT_FAILED",
					result.ErrorMessage ?? "The company payment failed.");
		}
		else
		{
			payment.ScheduleCompanyPaymentRetry(
				clock.UtcNow,
				result.ErrorCode,
				result.ErrorMessage);
		}

		await SaveAsync(payment, cancellationToken);
		return false;
	}

	private async Task<bool> ChargeCardAsync(
		Payment payment,
		CancellationToken cancellationToken)
	{
		if (payment.ChargeFirst && payment.ChargeAttempts >= payment.MaxChargeAttempts)
		{
			payment.MarkManualIntervention(clock.UtcNow, "CHARGE_REVIEW_REQUIRED", "The charge outcome requires operational review.");
			await SaveAsync(payment, cancellationToken);
			return false;
		}
		if (payment.ChargeFirst && payment.Status == PaymentTransactionStatus.ChargingCard)
			payment.MarkChargeUnknown(clock.UtcNow, payment.OperatorPaymentId, "CHARGE_INTERRUPTED", "The previous charge outcome is unconfirmed.");

		PaymentOperationResult result;
		if (payment.Status == PaymentTransactionStatus.ChargeOutcomeUnknown
			&& !string.IsNullOrWhiteSpace(payment.OperatorPaymentId))
		{
			payment.BeginChargeReconciliation(clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			result = await cardPaymentGateway.ReconcileChargeAsync(payment, cancellationToken);
		}
		else
		{
			if (payment.Status == PaymentTransactionStatus.ChargingCard)
			{
				payment.MarkChargeUnknown(
					clock.UtcNow,
					payment.OperatorPaymentId,
					"CHARGE_INTERRUPTED",
					"A previous charge attempt did not record an outcome.");
			}

			payment.BeginCardCharge(clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			result = await cardPaymentGateway.ChargeAsync(
				payment,
				payment.CreditCard,
				cancellationToken);
		}

		if (result.Outcome == PaymentOperationOutcome.Succeeded
			&& !string.IsNullOrWhiteSpace(result.ExternalReference))
		{
			if (payment.ChargeFirst) payment.RecordConfirmedCharge(result.ExternalReference, clock.UtcNow);
			else payment.MarkCompleted(result.ExternalReference, clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			return payment.ChargeFirst;
		}

		if (result.Outcome == PaymentOperationOutcome.Unknown
			|| result.Outcome == PaymentOperationOutcome.Succeeded)
		{
			if (payment.ChargeAttempts >= payment.MaxChargeAttempts)
			{
				payment.MarkManualIntervention(
					clock.UtcNow,
					result.ErrorCode ?? "CHARGE_OUTCOME_UNKNOWN",
					result.ErrorMessage ?? "The charge outcome could not be confirmed safely.");
				await SaveAsync(payment, cancellationToken);
				return false;
			}

			payment.MarkChargeUnknown(
				clock.UtcNow,
				result.ExternalReference,
				result.ErrorCode ?? "CHARGE_OUTCOME_UNKNOWN",
				result.ErrorMessage ?? "The charge outcome is unknown.");
			await SaveAsync(payment, cancellationToken);
			return false;
		}

		if (result.Outcome == PaymentOperationOutcome.RetryableFailure
			&& payment.ChargeAttempts < payment.MaxChargeAttempts)
		{
			payment.ScheduleChargeRetry(
				clock.UtcNow,
				result.ErrorCode,
				result.ErrorMessage);
			await SaveAsync(payment, cancellationToken);
			return false;
		}

		if (payment.BillKey is not null && result.Outcome == PaymentOperationOutcome.RetryableFailure)
		{
			payment.MarkManualIntervention(clock.UtcNow, "CHARGE_OUTCOME_UNKNOWN",
				"The charge outcome must be confirmed before releasing this bill.");
			await SaveAsync(payment, cancellationToken);
			return false;
		}

		if (payment.ChargeFirst)
		{
			payment.MarkFailed(clock.UtcNow, result.ErrorCode ?? "CARD_CHARGE_DECLINED", "The card charge was declined.");
			await SaveAsync(payment, cancellationToken);
			return false;
		}

		payment.BeginCompanyRollback(
			clock.UtcNow,
			result.ErrorCode ?? "CARD_CHARGE_FAILED",
			result.ErrorMessage ?? "The card charge failed.");
		await SaveAsync(payment, cancellationToken);
		return await RollbackCompanyAsync(payment, cancellationToken);
	}

	private async Task<bool> NotifyCompanyAsync(Payment payment, CancellationToken cancellationToken)
	{
		if (payment.CompanyPaymentAttempts >= payment.MaxCompanyPaymentAttempts)
		{
			payment.AwaitCompanyAcknowledgment(clock.UtcNow);
			await SaveAsync(payment, cancellationToken);
			return false;
		}

		var recover = payment.CompanyPaymentAttempts != 0;
		payment.BeginCompanyNotification(clock.UtcNow);
		await SaveAsync(payment, cancellationToken);
		var result = await chargeFirstGateway!.NotifyAsync(payment, recover, cancellationToken);
		if (result.Outcome == PaymentOperationOutcome.Succeeded && result.ExternalReference is { Length: > 0 } reference)
			payment.RecordCompanyAcknowledgment(reference, clock.UtcNow);
		else if (result.Outcome == PaymentOperationOutcome.DefinitiveFailure)
			payment.RequireCompensation(clock.UtcNow, result.ErrorMessage);
		else
			payment.AwaitCompanyAcknowledgment(clock.UtcNow);
		await SaveAsync(payment, cancellationToken);
		return payment.Status == PaymentTransactionStatus.CompensationPending;
	}

	private async Task<bool> CompensateAsync(Payment payment, CancellationToken cancellationToken)
	{
		if (payment.CompensationAttempts >= payment.MaxCompensationAttempts)
		{
			payment.AwaitCompensation(clock.UtcNow, requiresReview: true);
			await SaveAsync(payment, cancellationToken);
			return false;
		}

		payment.BeginCompensation(clock.UtcNow);
		await SaveAsync(payment, cancellationToken);
		var result = await chargeFirstGateway!.RefundAsync(payment, cancellationToken);
		if (result.Outcome == PaymentOperationOutcome.Succeeded && result.ExternalReference is { Length: > 0 } refundId)
			payment.RecordConfirmedCompensation(refundId, clock.UtcNow);
		else
			payment.AwaitCompensation(clock.UtcNow, result.Outcome == PaymentOperationOutcome.DefinitiveFailure);
		await SaveAsync(payment, cancellationToken);
		return false;
	}

	private async Task<bool> RollbackCompanyAsync(
		Payment payment,
		CancellationToken cancellationToken)
	{
		if (payment.Status == PaymentTransactionStatus.RollbackRetryScheduled)
		{
			payment.BeginCompanyRollback(clock.UtcNow, payment.ErrorCode, payment.ErrorMessage);
		}

		await SaveAsync(payment, cancellationToken);
		var result = await companyPaymentGateway.RollbackAsync(payment, cancellationToken);
		if (result.Outcome == PaymentOperationOutcome.Succeeded)
		{
			payment.MarkCompanyRolledBack(
				clock.UtcNow,
				payment.ErrorCode,
				payment.ErrorMessage);
		}
		else if (result.Outcome == PaymentOperationOutcome.DefinitiveFailure
			|| payment.RollbackAttempts >= payment.MaxRollbackAttempts)
		{
			payment.MarkManualIntervention(
				clock.UtcNow,
				result.ErrorCode ?? "COMPANY_ROLLBACK_FAILED",
				result.ErrorMessage ?? "Company rollback requires manual intervention.");
		}
		else
		{
			payment.ScheduleRollbackRetry(
				clock.UtcNow,
				result.ErrorCode,
				result.ErrorMessage);
		}

		await SaveAsync(payment, cancellationToken);
		return false;
	}

	private async Task SaveAsync(Payment payment, CancellationToken cancellationToken)
	{
		await paymentRepository.UpdateAsync(payment, cancellationToken);
		await unitOfWork.SaveChangesAsync(cancellationToken);
	}
}
