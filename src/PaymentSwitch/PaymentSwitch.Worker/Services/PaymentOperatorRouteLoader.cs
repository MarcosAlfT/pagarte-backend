using ExternalConnections.PaymentOperators.Config;
using ExternalConnections.PaymentOperators.PaymentOperators;
using Microsoft.Data.SqlClient;

namespace PaymentSwitch.Worker.Services;

// Read-only projection of Processor-owned routes; Worker never writes or creates pins.
public sealed class PaymentOperatorRouteLoader(IConfiguration configuration, PaymentOperatorRegistry registry, PaymentOperatorRouteGuard guard)
{
	public async Task LoadAsync(CancellationToken ct = default)
	{
		var configured = registry.GetConfigured();
		if (configured.Count == 0) { guard.Initialize([]); return; }
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				var pinned = await ReadAsync(ct);
				if (configured.Any(route => pinned.All(r => r.IntegrationId != route.Id)))
				{
					if (attempt >= 29) throw new InvalidOperationException("Start Processor with the retained integrations before Worker.");
				}
				else
				{
					// Validate before publishing a guard snapshot used by refund execution.
					var candidate = new PaymentOperatorRouteGuard();
					candidate.Initialize(pinned);
					foreach (var route in configured) candidate.Validate(new(route.Id, route.Integration.Adapter, route.Integration.Account, route.Integration.Environment, route.Integration.Version, route.Integration.CredentialReference));
					guard.Initialize(pinned);
					return;
				}
			}
			catch (SqlException error) when (error.Number == 208 && attempt < 29) { }
			await Task.Delay(TimeSpan.FromSeconds(1), ct);
		}
	}

	private async Task<IReadOnlyList<OperatorCardIdentity>> ReadAsync(CancellationToken ct)
	{
		await using var connection = new SqlConnection(configuration.GetConnectionString("PaymentDb"));
		await connection.OpenAsync(ct);
		await using var command = new SqlCommand("SELECT IntegrationId, Provider, Account, Environment, Version, CredentialReference FROM PaymentOperatorRoutes", connection);
		await using var reader = await command.ExecuteReaderAsync(ct);
		var pinned = new List<OperatorCardIdentity>();
		while (await reader.ReadAsync(ct)) pinned.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
		return pinned;
	}
}
