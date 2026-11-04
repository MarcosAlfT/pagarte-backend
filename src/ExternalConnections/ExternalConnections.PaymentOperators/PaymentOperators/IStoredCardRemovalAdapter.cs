namespace ExternalConnections.PaymentOperators.PaymentOperators;

public sealed record OperatorCardIdentity(string IntegrationId, string Provider, string Account, string Environment, string Version, string CredentialReference = "");
public sealed record OperatorCardRemovalCapabilities(bool Supported, bool RefundsSurviveDetachment);
public enum OperatorCardRemovalOutcome { Removed, Pending, Unsupported, Unknown, Rejected }

// Removal must be idempotent for its stable operation key, including after a lost response.
// Setup cleanup may remove only a resource exclusively owned by that setup, never a shared saved card.
// RefundsSurviveDetachment is an explicit provider guarantee, not inferred from a successful deletion.
public interface IStoredCardRemovalAdapter
{
	Task<OperatorCardRemovalOutcome> GetRemovalStatusAsync(OperatorCardIdentity identity, string reference, CancellationToken cancellationToken);
	OperatorCardRemovalCapabilities GetRemovalCapabilities(OperatorCardIdentity identity);
	Task<OperatorCardRemovalOutcome> RemoveCardAsync(OperatorCardIdentity identity, string reference, string operationKey, CancellationToken cancellationToken);
	Task<OperatorCardRemovalOutcome> RemoveSetupResourceAsync(OperatorSetupContext context, CancellationToken cancellationToken);
}
