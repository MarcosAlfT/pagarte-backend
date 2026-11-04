namespace ExternalConnections.PaymentOperators.PaymentOperators;

// Provider transport contract. Processor maps these into its application-owned models.
public sealed record OperatorSetupContext(Guid SessionId, string ClientId, string IntegrationId,
	string Account, string Environment, string Version, DateTime ExpiresAt)
{
	public string OperationKey => $"setup-{SessionId:N}";
}
public enum OperatorSetupOutcome { Succeeded, RequiresAction, Declined, Cancelled, Unknown, NotFound }
public sealed record OperatorSavedCard(string Reference, string Last4, string CardType, int ExpiryMonth, int ExpiryYear);
public sealed record OperatorSetupResult(OperatorSetupOutcome Outcome, OperatorSavedCard? Card = null, string? NextAction = null);

public sealed record OperatorSetupNotification(Guid SessionId, string EventId);

public interface ITokenizedCardSetupAdapter
{
	OperatorSetupNotification? VerifySetupNotification(string integrationId, IReadOnlyDictionary<string, string> headers, byte[] payload, DateTime utcNow);
	bool SupportsTokenizedSaving { get; }
	bool SupportsSetupRecovery { get; }
	bool SupportsDetachment { get; }
	bool SupportsSetupChallenges { get; }
	string IntegrationVersion { get; }
	string MobileMethod { get; }
	string MobileVersion { get; }
	Task<OperatorSetupResult> SaveTokenAsync(OperatorSetupContext context, string token, CancellationToken cancellationToken);
	Task<OperatorSetupResult> GetSetupStatusAsync(OperatorSetupContext context, CancellationToken cancellationToken);
}
