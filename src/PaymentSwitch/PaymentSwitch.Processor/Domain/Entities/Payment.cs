using PaymentSwitch.Messaging;

namespace PaymentSwitch.Processor.Domain.Entities;

public sealed partial class Payment
{
	public const string StaleBillVerification = "BILL_VERIFICATION_STALE";
	public string? BillKey { get; private set; }
	public string? BillVerificationJson { get; private set; }
	public DateTime? BillVerificationExpiresAt { get; private set; }
	public bool BillReservationReleased { get; private set; }
	public bool NeedsBillVerification => BillKey is not null && Status == PaymentTransactionStatus.CardReady &&
		CompanyPaymentAttempts == 0 && ChargeAttempts == 0 && ErrorCode == StaleBillVerification;

	public void SetBillVerification(string billKey, string json, DateTime expiresAt, DateTime utcNow)
	{
		if (BillKey is not null && (!NeedsBillVerification || BillKey != billKey))
			throw new InvalidOperationException("Bill verification can only be refreshed before execution starts.");
		BillKey = billKey;
		BillVerificationJson = json;
		BillVerificationExpiresAt = expiresAt;
		NextAttemptAt = utcNow;
		ClearError(utcNow);
	}

	public void RequestBillVerification(DateTime utcNow)
	{
		RequireStatus(PaymentTransactionStatus.CardReady);
		NextAttemptAt = null;
		SetError(StaleBillVerification, "The bill must be verified again before payment.", utcNow);
	}

	public Guid Id { get; set; }
	public string ClientId { get; set; } = string.Empty;
	public Guid SourceQuoteId { get; set; }
	public string PackageFingerprint { get; set; } = string.Empty;
	public Guid CreditCardId { get; set; }
	public Guid SourceServiceId { get; set; }
	public string ServiceName { get; set; } = string.Empty;
	public Guid CountryId { get; set; }
	public string CountryCode { get; set; } = string.Empty;
	public Guid RouteId { get; set; }
	public string RouteType { get; set; } = string.Empty;
	public Guid? PaymentNetworkId { get; set; }
	public Guid? ProviderId { get; set; }
	public string? ExternalSourceCode { get; set; }
	public string ExternalServiceCode { get; set; } = string.Empty;
	public string CompanyPaymentEndpoint { get; set; } = string.Empty;
	public string CompanyRollbackEndpoint { get; set; } = string.Empty;
	public string? CompanyStatusEndpoint { get; set; }
	public string CompanyCredentialReference { get; set; } = string.Empty;
	public long TotalMinorUnits { get; set; }
	public string Currency { get; set; } = string.Empty;
	public int CurrencyScale { get; set; }
	public string RoundingMode { get; set; } = string.Empty;
	public DateTime QuoteCreatedAt { get; set; }
	public DateTime QuoteExpiresAt { get; set; }

	public string OperatorProvider { get; set; } = string.Empty;
	public string? OperatorPaymentId { get; set; }
	public string? CompanyReference { get; set; }
	public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Accepted;
	public string Reference { get; set; } = string.Empty;
	public string? ErrorCode { get; set; }
	public string? ErrorMessage { get; set; }

	public string CompanyPaymentIdempotencyKey { get; set; } = string.Empty;
	public string CardChargeIdempotencyKey { get; set; } = string.Empty;
	public string CompanyRollbackIdempotencyKey { get; set; } = string.Empty;
	public int CardCheckAttempts { get; set; }
	public int CompanyPaymentAttempts { get; set; }
	public int ChargeAttempts { get; set; }
	public int RollbackAttempts { get; set; }
	public int MaxCompanyPaymentAttempts { get; set; }
	public int MaxCardCheckAttempts { get; set; }
	public int MaxChargeAttempts { get; set; }
	public int MaxRollbackAttempts { get; set; }
	public int RetryIntervalSeconds { get; set; }
	public DateTime? NextAttemptAt { get; set; }
	public int RefundRetryCount { get; set; }
	public DateTime? NextRefundRetryAt { get; set; }

	public DateTime CreatedAt { get; set; }
	public DateTime? ProcessedAt { get; set; }
	public DateTime? LastUpdatedAt { get; set; }
	public byte[] Version { get; set; } = [];

	public CreditCard CreditCard { get; set; } = null!;
	public ICollection<PaymentDetail> Details { get; set; } = [];
	public ICollection<PaymentReferenceValue> ReferenceValues { get; set; } = [];

	public bool IsFinal => Status is PaymentTransactionStatus.Completed
		or PaymentTransactionStatus.Compensated
		or PaymentTransactionStatus.Failed
		or PaymentTransactionStatus.CompanyRolledBack
		or PaymentTransactionStatus.ManualIntervention;

	public static Payment Create(PaymentCreationData data, DateTime utcNow)
	{
		if (data.TotalMinorUnits <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(data),
				"Payment total must be greater than zero.");
		}

		if (data.MaxCardCheckAttempts <= 0
			|| data.MaxChargeAttempts <= 0
			|| data.MaxCompanyPaymentAttempts <= 0
			|| data.MaxRollbackAttempts <= 0
			|| data.RetryIntervalSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(data),
				"Retry policy values must be greater than zero.");
		}

		var id = Guid.NewGuid();
		return new Payment
		{
			Id = id,
			ClientId = data.ClientId,
			SourceQuoteId = data.SourceQuoteId,
			PackageFingerprint = data.PackageFingerprint,
			CreditCardId = data.CreditCardId,
			SourceServiceId = data.SourceServiceId,
			ServiceName = data.ServiceName,
			CountryId = data.CountryId,
			CountryCode = data.CountryCode,
			RouteId = data.RouteId,
			RouteType = data.RouteType,
			PaymentNetworkId = data.PaymentNetworkId,
			ProviderId = data.ProviderId,
			ExternalSourceCode = data.ExternalSourceCode,
			ExternalServiceCode = data.ExternalServiceCode,
			CompanyPaymentEndpoint = data.CompanyPaymentEndpoint,
			CompanyRollbackEndpoint = data.CompanyRollbackEndpoint,
			CompanyStatusEndpoint = data.CompanyStatusEndpoint,
			CompanyCredentialReference = data.CompanyCredentialReference,
			TotalMinorUnits = data.TotalMinorUnits,
			Currency = data.Currency,
			CurrencyScale = data.CurrencyScale,
			RoundingMode = data.RoundingMode,
			QuoteCreatedAt = data.QuoteCreatedAt,
			QuoteExpiresAt = data.QuoteExpiresAt,
			OperatorProvider = data.OperatorProvider,
			Status = PaymentTransactionStatus.Accepted,
			Reference = GenerateReference(utcNow),
			CompanyPaymentIdempotencyKey = IdempotencyKey(id, "company"),
			CardChargeIdempotencyKey = IdempotencyKey(id, "charge"),
			CompanyRollbackIdempotencyKey = IdempotencyKey(id, "rollback"),
			MaxCompanyPaymentAttempts = data.MaxCompanyPaymentAttempts,
			MaxCardCheckAttempts = data.MaxCardCheckAttempts,
			MaxChargeAttempts = data.MaxChargeAttempts,
			MaxRollbackAttempts = data.MaxRollbackAttempts,
			RetryIntervalSeconds = data.RetryIntervalSeconds,
			NextAttemptAt = utcNow,
			CreatedAt = utcNow,
			LastUpdatedAt = utcNow
		};
	}

	public void BeginCardCheck(DateTime utcNow)
	{
		RequireStatus(PaymentTransactionStatus.Accepted, PaymentTransactionStatus.CardCheckRetryScheduled);
		Status = PaymentTransactionStatus.CheckingCard;
		CardCheckAttempts++;
		NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
		ClearError(utcNow);
	}

	public void MarkCardReady(DateTime utcNow)
	{
		RequireStatus(PaymentTransactionStatus.CheckingCard);
		Status = PaymentTransactionStatus.CardReady;
		NextAttemptAt = utcNow;
		ClearError(utcNow);
	}

	public void ScheduleCardCheckRetry(
		DateTime utcNow,
		string? errorCode,
		string? errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.CheckingCard);
		Status = PaymentTransactionStatus.CardCheckRetryScheduled;
		Schedule(utcNow, errorCode, errorMessage);
	}

	public void BeginCompanyPayment(DateTime utcNow)
	{
		RequireStatus(
			PaymentTransactionStatus.CardReady,
			PaymentTransactionStatus.CompanyPaymentRetryScheduled,
			PaymentTransactionStatus.CompanyPaymentOutcomeUnknown);
		Status = PaymentTransactionStatus.PayingCompany;
		CompanyPaymentAttempts++;
		NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
		ClearError(utcNow);
	}

	public void BeginCompanyPaymentReconciliation(DateTime utcNow)
	{
		RequireStatus(PaymentTransactionStatus.CompanyPaymentOutcomeUnknown);
		Status = PaymentTransactionStatus.PayingCompany;
		NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
		ClearError(utcNow);
	}

	public void MarkCompanyPaymentUnknown(
		DateTime utcNow,
		string? companyReference,
		string? errorCode,
		string? errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.PayingCompany);
		CompanyReference = companyReference ?? CompanyReference;
		Status = PaymentTransactionStatus.CompanyPaymentOutcomeUnknown;
		Schedule(utcNow, errorCode, errorMessage);
	}

	public void ScheduleCompanyPaymentRetry(
		DateTime utcNow,
		string? errorCode,
		string? errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.PayingCompany);
		Status = PaymentTransactionStatus.CompanyPaymentRetryScheduled;
		Schedule(utcNow, errorCode, errorMessage);
	}

	public void MarkCompanyPaid(string companyReference, DateTime utcNow)
	{
		RequireStatus(
			PaymentTransactionStatus.PayingCompany,
			PaymentTransactionStatus.CompanyPaymentOutcomeUnknown);
		CompanyReference = companyReference;
		Status = PaymentTransactionStatus.CompanyPaid;
		NextAttemptAt = utcNow;
		ClearError(utcNow);
	}

	public void BeginCardCharge(DateTime utcNow)
	{
		RequireStatus(
			ChargeFirst ? PaymentTransactionStatus.CardReady : PaymentTransactionStatus.CompanyPaid,
			PaymentTransactionStatus.ChargeRetryScheduled,
			PaymentTransactionStatus.ChargeOutcomeUnknown);
		Status = PaymentTransactionStatus.ChargingCard;
		ChargeAttempts++;
		NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
		ClearError(utcNow);
	}

	public void BeginChargeReconciliation(DateTime utcNow)
	{
		RequireStatus(PaymentTransactionStatus.ChargeOutcomeUnknown);
		Status = PaymentTransactionStatus.ChargingCard;
		if (ChargeFirst) ChargeAttempts++;
		NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
		ClearError(utcNow);
	}

	public void MarkChargeUnknown(
		DateTime utcNow,
		string? operatorPaymentId,
		string? errorCode,
		string? errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.ChargingCard);
		OperatorPaymentId = operatorPaymentId ?? OperatorPaymentId;
		Status = PaymentTransactionStatus.ChargeOutcomeUnknown;
		Schedule(utcNow, errorCode, errorMessage);
	}

	public void ScheduleChargeRetry(
		DateTime utcNow,
		string? errorCode,
		string? errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.ChargingCard);
		Status = PaymentTransactionStatus.ChargeRetryScheduled;
		Schedule(utcNow, errorCode, errorMessage);
	}

	public void MarkCompleted(string operatorPaymentId, DateTime utcNow)
	{
		if (ChargeFirst) throw new InvalidOperationException("Company acknowledgment is required.");
		RequireStatus(PaymentTransactionStatus.ChargingCard, PaymentTransactionStatus.ChargeOutcomeUnknown);
		OperatorPaymentId = operatorPaymentId;
		Status = PaymentTransactionStatus.Completed;
		ProcessedAt = utcNow;
		NextAttemptAt = null;
		ClearError(utcNow);
	}

	public void BeginCompanyRollback(DateTime utcNow, string? errorCode, string? errorMessage)
	{
		if (Status is not (PaymentTransactionStatus.ChargingCard
			or PaymentTransactionStatus.ChargeRetryScheduled
			or PaymentTransactionStatus.ChargeOutcomeUnknown
			or PaymentTransactionStatus.RollbackRetryScheduled))
		{
			throw new InvalidOperationException($"Cannot begin rollback from status {Status}.");
		}

		Status = PaymentTransactionStatus.RollingBackCompany;
		RollbackAttempts++;
		NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
		SetError(errorCode, errorMessage, utcNow);
	}

	public void ScheduleRollbackRetry(
		DateTime utcNow,
		string? errorCode,
		string? errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.RollingBackCompany);
		Status = PaymentTransactionStatus.RollbackRetryScheduled;
		Schedule(utcNow, errorCode, errorMessage);
	}

	public void MarkCompanyRolledBack(DateTime utcNow, string? errorCode, string? errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.RollingBackCompany);
		Status = PaymentTransactionStatus.CompanyRolledBack;
		BillReservationReleased = BillKey is not null;
		ProcessedAt = utcNow;
		NextAttemptAt = null;
		SetError(errorCode ?? "CARD_CHARGE_FAILED", errorMessage, utcNow);
	}

	public void MarkFailed(DateTime utcNow, string errorCode, string errorMessage)
	{
		Status = PaymentTransactionStatus.Failed;
		BillReservationReleased = BillKey is not null && CompanyPaymentAttempts == 0;
		ProcessedAt = utcNow;
		NextAttemptAt = null;
		SetError(errorCode, errorMessage, utcNow);
	}

	public void MarkFirstCompanyPaymentRejected(DateTime utcNow, string errorCode, string errorMessage)
	{
		RequireStatus(PaymentTransactionStatus.PayingCompany);
		if (CompanyPaymentAttempts != 1)
			throw new InvalidOperationException("The first company payment attempt is required.");
		MarkFailed(utcNow, errorCode, errorMessage);
		BillReservationReleased = BillKey is not null;
	}

	public void MarkManualIntervention(DateTime utcNow, string errorCode, string errorMessage)
	{
		Status = PaymentTransactionStatus.ManualIntervention;
		ProcessedAt = utcNow;
		NextAttemptAt = null;
		SetError(errorCode, errorMessage, utcNow);
	}

	private void Schedule(DateTime utcNow, string? errorCode, string? errorMessage)
	{
		NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
		SetError(errorCode, errorMessage, utcNow);
	}

	private void ClearError(DateTime utcNow)
	{
		ErrorCode = null;
		ErrorMessage = null;
		LastUpdatedAt = utcNow;
	}

	private void SetError(string? errorCode, string? errorMessage, DateTime utcNow)
	{
		ErrorCode = errorCode;
		ErrorMessage = errorMessage;
		LastUpdatedAt = utcNow;
	}

	private void RequireStatus(params PaymentTransactionStatus[] allowed)
	{
		if (!allowed.Contains(Status))
		{
			throw new InvalidOperationException(
				$"Payment status {Status} does not allow this transition.");
		}
	}

	private static string GenerateReference(DateTime utcNow) =>
		$"PAG-{utcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

	private static string IdempotencyKey(Guid paymentId, string operation) =>
		$"{operation}-{paymentId:N}"[..Math.Min(42, operation.Length + 33)];
}

public sealed record PaymentCreationData(
	string ClientId,
	Guid SourceQuoteId,
	string PackageFingerprint,
	Guid CreditCardId,
	Guid SourceServiceId,
	string ServiceName,
	Guid CountryId,
	string CountryCode,
	Guid RouteId,
	string RouteType,
	Guid? PaymentNetworkId,
	Guid? ProviderId,
	string? ExternalSourceCode,
	string ExternalServiceCode,
	string CompanyPaymentEndpoint,
	string CompanyRollbackEndpoint,
	string? CompanyStatusEndpoint,
	string CompanyCredentialReference,
	long TotalMinorUnits,
	string Currency,
	int CurrencyScale,
	string RoundingMode,
	DateTime QuoteCreatedAt,
	DateTime QuoteExpiresAt,
	string OperatorProvider,
	int MaxCardCheckAttempts,
	int MaxCompanyPaymentAttempts,
	int MaxChargeAttempts,
	int MaxRollbackAttempts,
	int RetryIntervalSeconds);
