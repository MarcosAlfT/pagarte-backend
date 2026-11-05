using PaymentSwitch.Messaging;

namespace PaymentSwitch.Processor.Domain.Entities;

public sealed partial class Payment
{
    public bool ChargeFirst { get; private set; }
    public string? CompanyNotificationEndpoint { get; private set; }
    public string? BillingPeriod { get; private set; }
    public string? CompensationKey { get; private set; }
    public string? OperatorRefundId { get; private set; }
    public int CompensationAttempts { get; private set; }
    public int MaxCompensationAttempts { get; private set; }

    public void PinChargeFirst(string endpoint, string billingPeriod, int maxRefundAttempts)
    {
        RequireStatus(PaymentTransactionStatus.Accepted);
        if (ChargeFirst || BillKey is null || maxRefundAttempts <= 0)
            throw new InvalidOperationException("Charge-first execution requires a verified bill and a refund policy.");
        ChargeFirst = true;
        CompanyNotificationEndpoint = endpoint;
        BillingPeriod = billingPeriod;
        CompensationKey = IdempotencyKey(Id, "refund");
        MaxCompensationAttempts = maxRefundAttempts;
    }

    public void RecordConfirmedCharge(string operatorPaymentId, DateTime utcNow)
    {
        RequireStatus(PaymentTransactionStatus.ChargingCard);
        OperatorPaymentId = operatorPaymentId;
        Status = PaymentTransactionStatus.ChargedAwaitingCompany;
        NextAttemptAt = utcNow;
        ClearError(utcNow);
    }

    public void BeginCompanyNotification(DateTime utcNow)
    {
        RequireStatus(PaymentTransactionStatus.ChargedAwaitingCompany, PaymentTransactionStatus.NotifyingCompany);
        Status = PaymentTransactionStatus.NotifyingCompany;
        CompanyPaymentAttempts++;
        NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
    }

    public void RecordCompanyAcknowledgment(string reference, DateTime utcNow)
    {
        RequireStatus(PaymentTransactionStatus.NotifyingCompany);
        CompanyReference = reference;
        FinishChargeFirst(PaymentTransactionStatus.Completed, utcNow);
    }

    public void AwaitCompanyAcknowledgment(DateTime utcNow)
    {
        RequireStatus(PaymentTransactionStatus.NotifyingCompany);
        Status = PaymentTransactionStatus.ChargedAwaitingCompany;
        Schedule(utcNow, "COMPANY_ACKNOWLEDGMENT_UNKNOWN", "Company acknowledgment is still unconfirmed.");
        if (CompanyPaymentAttempts >= MaxCompanyPaymentAttempts)
        {
            NextAttemptAt = null;
            SetError("COMPANY_REVIEW_REQUIRED", "Company acknowledgment requires operational review.", utcNow);
        }
    }

    public void RequireCompensation(DateTime utcNow, string? reason)
    {
        RequireStatus(PaymentTransactionStatus.NotifyingCompany);
        Status = PaymentTransactionStatus.CompensationPending;
        NextAttemptAt = utcNow;
        SetError("COMPANY_NOTIFICATION_REJECTED", reason, utcNow);
    }

    public void BeginCompensation(DateTime utcNow)
    {
        RequireStatus(PaymentTransactionStatus.CompensationPending, PaymentTransactionStatus.RefundingCompensation);
        Status = PaymentTransactionStatus.RefundingCompensation;
        CompensationAttempts++;
        NextAttemptAt = utcNow.AddSeconds(RetryIntervalSeconds);
    }

    public void AwaitCompensation(DateTime utcNow, bool requiresReview)
    {
        RequireStatus(PaymentTransactionStatus.RefundingCompensation);
        Status = PaymentTransactionStatus.CompensationPending;
        Schedule(utcNow, "REFUND_UNCONFIRMED", "The full refund is still unconfirmed.");
        if (requiresReview || CompensationAttempts >= MaxCompensationAttempts)
        {
            NextAttemptAt = null;
            SetError("REFUND_REVIEW_REQUIRED", "The full refund requires operational review.", utcNow);
        }
    }

    public void RecordConfirmedCompensation(string refundId, DateTime utcNow)
    {
        RequireStatus(PaymentTransactionStatus.RefundingCompensation);
        OperatorRefundId = refundId;
        BillReservationReleased = true;
        FinishChargeFirst(PaymentTransactionStatus.Compensated, utcNow);
    }

    private void FinishChargeFirst(PaymentTransactionStatus status, DateTime utcNow)
    {
        Status = status;
        ProcessedAt = utcNow;
        NextAttemptAt = null;
        ClearError(utcNow);
    }
}
