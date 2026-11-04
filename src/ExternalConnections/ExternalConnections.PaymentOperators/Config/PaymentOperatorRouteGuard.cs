using ExternalConnections.PaymentOperators.PaymentOperators;

namespace ExternalConnections.PaymentOperators.Config;

// Hosts load Processor-owned pins before any configured integration can be used.
public sealed class PaymentOperatorRouteGuard
{
	private IReadOnlyDictionary<string, OperatorCardIdentity>? routes;
	public void Initialize(IEnumerable<OperatorCardIdentity> pinned) => routes = pinned.ToDictionary(r => r.IntegrationId, StringComparer.Ordinal);
	public void Validate(OperatorCardIdentity identity)
	{
		var snapshot = routes;
		if (snapshot is null || !snapshot.TryGetValue(identity.IntegrationId, out var original) || original != identity)
			throw new InvalidOperationException("The configured integration must match its persisted route. Use a new integration ID for a different identity.");
	}
}
