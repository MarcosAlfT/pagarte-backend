using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ExternalConnections.PaymentOperators.Config;
using Microsoft.Extensions.Hosting;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

// A separate synthetic provider journal survives Processor restarts. It contains no submitted tokens.
// It is a development fixture, not part of PaymentDb or the company mock.
public sealed class MockCardSetupJournal(IConfiguration configuration, IHostEnvironment environment, PaymentOperatorRegistry registry)
{
	private string DirectoryPath
	{
		get
		{
			if (!environment.IsDevelopment()) throw new InvalidOperationException("Mock card setup requires Development.");
			return configuration["PaymentOperators:MockStateDirectory"] ?? Path.Combine(Path.GetTempPath(), "pagarte-mock-card-setup");
		}
	}

	public OperatorSetupNotification? VerifyNotification(string integrationId, string timestamp, string signature, byte[] payload, DateTime utcNow)
	{
		_ = DirectoryPath; // Enforce the Development boundary for callbacks too.
		var integration = registry.GetRequired(integrationId);
		var key = configuration[$"PaymentOperatorCallbackSecrets:{integration.CallbackKeyReference}"];
		if (string.IsNullOrWhiteSpace(integration.CallbackKeyReference) || string.IsNullOrWhiteSpace(key) || key.Length < 32
			|| payload.Length > 8192 || signature.Length != 64 || !long.TryParse(timestamp, out var seconds)) return null;
		if (seconds < DateTimeOffset.MinValue.ToUnixTimeSeconds() || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) return null;
		var sentAt = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
		if (Math.Abs((utcNow - sentAt).TotalMinutes) > 5) return null;
		try
		{
			var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes("mock-callback-v1\n" + timestamp + ".").Concat(payload).ToArray());
			if (!CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(signature))) return null;
			using var document = JsonDocument.Parse(payload);
			var root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
				|| !root.TryGetProperty("sessionId", out var session) || session.ValueKind != JsonValueKind.String
				|| !Guid.TryParse(session.GetString(), out var id) || id == Guid.Empty
				|| !root.TryGetProperty("eventId", out var eventId) || eventId.ValueKind != JsonValueKind.String) return null;
			var value = eventId.GetString();
			return string.IsNullOrWhiteSpace(value) || value.Length > 100 ? null : new(id, value);
		}
		catch (Exception error) when (error is FormatException or JsonException) { return null; }
	}

	public OperatorCardRemovalCapabilities GetRemovalCapabilities(OperatorCardIdentity identity)
	{
		_ = DirectoryPath;
		var configured = registry.GetRequired(identity.IntegrationId);
		if (identity.Provider != "Mock" || configured.Adapter != identity.Provider || configured.Account != identity.Account
			|| configured.Environment != identity.Environment || configured.Version != identity.Version)
			throw new InvalidOperationException("The original mock account must remain configured.");
		return new(configured.CardRemovalEnabled, configured.RefundsSurviveDetachment);
	}

	public bool IsRemoved(OperatorCardIdentity identity, string reference) => File.Exists(RemovalPath(identity, reference));

	internal async Task<T?> ReadPaymentOperationAsync<T>(OperatorCardIdentity identity, string kind, string key, CancellationToken ct)
		where T : class
	{
		var path = PaymentOperationPath(identity, kind, key);
		if (!File.Exists(path)) return null;
		return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, ct));
	}

	internal async Task<bool> RecordPaymentOperationAsync<T>(OperatorCardIdentity identity, string kind, string key, T evidence,
		CancellationToken ct) where T : class
	{
		var path = PaymentOperationPath(identity, kind, key);
		var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		try
		{
			await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(evidence), ct);
			try { File.Move(temporaryPath, path, overwrite: false); }
			catch (IOException) when (File.Exists(path)) { }
		}
		finally
		{
			if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
		}
		return Equals(evidence, await ReadPaymentOperationAsync<T>(identity, kind, key, ct));
	}

	private string PaymentOperationPath(OperatorCardIdentity identity, string kind, string key)
	{
		var scope = JsonSerializer.SerializeToUtf8Bytes(new[] { identity.IntegrationId, identity.Account, identity.Environment,
			identity.Version, kind, key });
		return Path.Combine(DirectoryPath, Convert.ToHexString(SHA256.HashData(scope)) + ".payment.json");
	}

	public async Task<OperatorCardRemovalOutcome> RemoveCardAsync(OperatorCardIdentity identity, string reference, string operationKey, CancellationToken ct)
	{
		if (!GetRemovalCapabilities(identity).Supported) return OperatorCardRemovalOutcome.Unsupported;
		if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(operationKey)) return OperatorCardRemovalOutcome.Rejected;
		var path = RemovalPath(identity, reference);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		// No tokens or references are recorded in the tombstone contents.
		await File.WriteAllTextAsync(path, "removed", ct);
		return OperatorCardRemovalOutcome.Removed;
	}

	public async Task<OperatorCardRemovalOutcome> RemoveSetupResourceAsync(OperatorSetupContext context, CancellationToken ct)
	{
		var record = await ReadAsync(context, ct);
		if (record is null) return OperatorCardRemovalOutcome.Removed;
		// Shared references cannot safely be detached as setup-specific resources.
		if (record.Scenario == "same-instrument") return OperatorCardRemovalOutcome.Unsupported;
		var result = Result(context, record);
		if (result.Outcome != OperatorSetupOutcome.Succeeded || result.Card is null) return OperatorCardRemovalOutcome.Unsupported;
		return await RemoveCardAsync(Identity(context), result.Card.Reference, "cleanup-" + context.SessionId.ToString("N"), ct);
	}

	private static OperatorCardIdentity Identity(OperatorSetupContext context) => new(context.IntegrationId, "Mock", context.Account, context.Environment, context.Version);
	private string RemovalPath(OperatorCardIdentity identity, string reference) => Path.Combine(DirectoryPath,
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{identity.IntegrationId}|{identity.Account}|{identity.Environment}|{reference}"))) + ".removed");

	public async Task<OperatorSetupResult> SaveAsync(OperatorSetupContext context, string token, CancellationToken ct)
	{
		var prefix = $"mock:{context.IntegrationId}:{context.SessionId:N}:";
		if (!token.StartsWith(prefix, StringComparison.Ordinal)) return new(OperatorSetupOutcome.Declined);
		var scenario = token[prefix.Length..];
		if (!Scenarios.Contains(scenario)) return new(OperatorSetupOutcome.Declined);
		if (scenario == "unavailable") return new(OperatorSetupOutcome.Unknown);
		var path = GetPath(context);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var record = new Entry(context.ClientId, context.Account, context.Environment, context.Version, scenario);
		// Atomic publication: a reader never sees a partially written provider result.
		var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(record), ct);
			try
			{
				File.Move(temporaryPath, path, overwrite: false);
				// Only a distinct, explicit registration may reactivate a previously removed instrument.
				var result = Result(context, record);
				if (result.Card is not null) File.Delete(RemovalPath(Identity(context), result.Card.Reference));
			}
			catch (IOException) when (File.Exists(path)) { }
		}
		finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
		var stored = await ReadAsync(context, ct);
		if (stored is null || stored != record) return new(OperatorSetupOutcome.Declined);
		if (scenario.StartsWith("challenge-", StringComparison.Ordinal))
			return new(OperatorSetupOutcome.RequiresAction, NextAction: "PresentChallenge");
		if (scenario == "save-before-timeout") return new(OperatorSetupOutcome.Unknown);
		return Result(context, stored);
	}

	public async Task<OperatorSetupResult> GetStatusAsync(OperatorSetupContext context, CancellationToken ct)
	{
		var record = await ReadAsync(context, ct);
		if (record is null) return new(OperatorSetupOutcome.NotFound);
		var result = Result(context, record);
		return result.Card is not null && IsRemoved(Identity(context), result.Card.Reference) ? new(OperatorSetupOutcome.NotFound) : result;
	}

	private async Task<Entry?> ReadAsync(OperatorSetupContext context, CancellationToken ct)
	{
		var path = GetPath(context);
		if (!File.Exists(path)) return null;
		var record = JsonSerializer.Deserialize<Entry>(await File.ReadAllTextAsync(path, ct));
		return record is not null && record.ClientId == context.ClientId && record.Account == context.Account
			&& record.Environment == context.Environment && record.Version == context.Version ? record : null;
	}

	private string GetPath(OperatorSetupContext context)
	{
		var scope = $"{context.IntegrationId}|{context.Account}|{context.Environment}|{context.OperationKey}";
		return Path.Combine(DirectoryPath, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope))) + ".json");
	}

	private static OperatorSetupResult Result(OperatorSetupContext context, Entry record) => record.Scenario switch
	{
		"decline" or "challenge-decline" => new(OperatorSetupOutcome.Declined),
		"challenge-cancel" => new(OperatorSetupOutcome.Cancelled),
		"timeout" => new(OperatorSetupOutcome.Unknown),
		"challenge-expire" => new(OperatorSetupOutcome.RequiresAction, NextAction: "PresentChallenge"),
		"malformed" => new(OperatorSetupOutcome.Succeeded, new("", "invalid", "", 0, 0)),
		"same-instrument" => new(OperatorSetupOutcome.Succeeded, new("mock-card-" + Convert.ToHexString(
			SHA256.HashData(Encoding.UTF8.GetBytes(context.Account + "|" + context.ClientId + "|instrument-one"))), "4242", "Visa", 12, 2040)),
		_ => new(OperatorSetupOutcome.Succeeded, new($"mock-card-{context.SessionId:N}", "4242", "Visa", 12, 2040))
	};

	private static readonly HashSet<string> Scenarios = ["success", "decline", "challenge-success", "challenge-decline",
		"challenge-cancel", "challenge-expire", "timeout", "save-before-timeout", "unavailable", "malformed", "same-instrument"];
	private sealed record Entry(string ClientId, string Account, string Environment, string Version, string Scenario);
}
