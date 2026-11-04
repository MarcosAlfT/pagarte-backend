namespace PaymentSwitch.Processor.Application.Models;

public sealed record CardReviewItem(Guid Id, string Kind, string State, string ClientId, int Attempts, DateTime CreatedAt);
public sealed record ReviewCardCommand(string Actor, string Key, Guid Id, string Kind, string Action, string ExpectedState, string ReasonCode);
public sealed record CardReviewView(Guid Id, Guid TargetId, string Kind, string Action, string Actor, string ReasonCode,
	string BeforeState, string AfterState, string Outcome, DateTime CreatedAt, DateTime? CompletedAt);
