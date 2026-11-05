namespace PaymentSwitch.Processor.Domain.Entities;

public sealed class PaymentRecoveryAction
{
    public Guid Id { get; init; }
    public Guid PaymentId { get; init; }
    public string Actor { get; init; } = "";
    public string RequestKey { get; init; } = "";
    public string ReasonCode { get; init; } = "";
    public string ExpectedVersion { get; init; } = "";
    public PaymentRecoveryStage Stage { get; init; }
    public DateTime ScheduledAt { get; init; }
}
