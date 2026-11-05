using PaymentSwitch.Messaging;

namespace PaymentSwitch.Processor.Domain.Entities;

public enum PaymentRecoveryStage { Charge, CompanyAcknowledgment, Refund }

public sealed partial class Payment
{
    public void RequireChargeRecovery(DateTime utcNow, string? operatorPaymentId = null)
    {
        OperatorPaymentId = operatorPaymentId ?? OperatorPaymentId;
        MarkManualIntervention(utcNow, "CHARGE_REVIEW_REQUIRED", "The charge outcome requires operational review.");
    }

    public PaymentRecoveryStage? GetRecoveryStage()
    {
        if (!ChargeFirst || NextAttemptAt is not null) return null;
        return Status switch
        {
            PaymentTransactionStatus.ManualIntervention => PaymentRecoveryStage.Charge,
            PaymentTransactionStatus.ChargedAwaitingCompany => PaymentRecoveryStage.CompanyAcknowledgment,
            PaymentTransactionStatus.CompensationPending => PaymentRecoveryStage.Refund,
            _ => null
        };
    }

    public void ScheduleRecovery(PaymentRecoveryStage stage, DateTime utcNow)
    {
        if (GetRecoveryStage() != stage)
            throw new InvalidOperationException("Only a stalled charge-first operation can be scheduled for recovery.");

        switch (stage)
        {
            case PaymentRecoveryStage.Charge:
                Status = PaymentTransactionStatus.ChargeOutcomeUnknown;
                MaxChargeAttempts = checked(ChargeAttempts + 1);
                break;
            case PaymentRecoveryStage.CompanyAcknowledgment:
                MaxCompanyPaymentAttempts = checked(CompanyPaymentAttempts + 1);
                break;
            case PaymentRecoveryStage.Refund:
                MaxCompensationAttempts = checked(CompensationAttempts + 1);
                break;
        }
        ProcessedAt = null;
        NextAttemptAt = utcNow;
        SetError("PAYMENT_RECOVERY_SCHEDULED", "Recovery of the original payment operation is scheduled.", utcNow);
    }
}
