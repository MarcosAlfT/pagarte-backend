using System.Net;
using System.Text.Json;

namespace ExternalConnections.PaymentOperators.PaymentOperators.DLocal;

// Supplied by a trusted backend identity/binding component, never by the public card request.
public sealed class DLocalPayer
{
	public required string ClientId { get; init; }
	public required string Name { get; init; }
	public required string Email { get; init; }
	public required string Document { get; init; }
	public override string ToString() => "dLocal payer [redacted]";
}

public sealed class DLocalCardVaultClient(DLocalApiClient api)
{
	// Low-level preparation only: Direct-token compatibility and customer/session binding must be
	// established before an application setup adapter may call this method for public registration.
	public async Task<OperatorSetupResult> SaveTokenAsync(string country, string token, DLocalPayer payer, string operationKey, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(country) || country.Length != 2 || !country.All(char.IsAsciiLetterUpper) || string.IsNullOrWhiteSpace(token) || token.Length > 4096 || token.Any(char.IsControl) || token.All(char.IsAsciiDigit)
			|| new[] { payer.ClientId, payer.Name, payer.Email, payer.Document }.Any(v => string.IsNullOrWhiteSpace(v) || v.Length > 200 || v.Any(char.IsControl)))
			throw new InvalidOperationException("Supply a token and trusted, validated payer identity for the processing country.");
		try
		{
			using var response = await api.SendAsync(HttpMethod.Post, "/secure_cards", new {
				country, card = new { token }, payer = new { name = payer.Name, email = payer.Email, document = payer.Document, user_reference = payer.ClientId }
			}, operationKey, ct);
			if (!response.IsSuccessStatusCode) return new(IsDefinitiveCardRejection(response.StatusCode) ? OperatorSetupOutcome.Declined : OperatorSetupOutcome.Unknown);
			return Parse(await DLocalApiClient.ReadJsonAsync(response, ct));
		}
		catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
		{ return new(OperatorSetupOutcome.Unknown); }
	}

	public async Task<OperatorSetupResult> GetCardAsync(string reference, CancellationToken ct)
	{
		ValidateReference(reference);
		try
		{
			using var response = await api.SendAsync(HttpMethod.Get, "/cards/" + Uri.EscapeDataString(reference), null, null, ct);
			if (response.StatusCode == HttpStatusCode.NotFound) return new(OperatorSetupOutcome.NotFound);
			if (!response.IsSuccessStatusCode) return new(OperatorSetupOutcome.Unknown);
			var parsed = Parse(await DLocalApiClient.ReadJsonAsync(response, ct));
			return parsed.Card?.Reference == reference ? parsed : new(OperatorSetupOutcome.Unknown);
		}
		catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
		{ return new(OperatorSetupOutcome.Unknown); }
	}

	public async Task<OperatorCardRemovalOutcome> RemoveAsync(string reference, string operationKey, CancellationToken ct)
	{
		ValidateReference(reference);
		try
		{
			using var response = await api.SendAsync(HttpMethod.Delete, "/secure_cards/" + Uri.EscapeDataString(reference), null, operationKey, ct);
			if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent) return OperatorCardRemovalOutcome.Removed;
			if (!response.IsSuccessStatusCode) return IsDefinitiveCardRejection(response.StatusCode) ? OperatorCardRemovalOutcome.Rejected : OperatorCardRemovalOutcome.Unknown;
			// A 2xx alone is not evidence of removal: verify authoritative absence.
			return (await GetCardAsync(reference, ct)).Outcome == OperatorSetupOutcome.NotFound
				? OperatorCardRemovalOutcome.Removed : OperatorCardRemovalOutcome.Unknown;
		}
		catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
		{ return OperatorCardRemovalOutcome.Unknown; }
	}

	public static OperatorSetupResult Parse(JsonElement body)
	{
		if (body.ValueKind != JsonValueKind.Object) return new(OperatorSetupOutcome.Unknown);
		if (body.TryGetProperty("status", out var rootStatus) && (rootStatus.ValueKind != JsonValueKind.String || rootStatus.GetString() != "ACTIVE")) return new(OperatorSetupOutcome.Unknown);
		var card = body.TryGetProperty("card", out var nested) ? nested : body;
		if (card.ValueKind != JsonValueKind.Object) return new(OperatorSetupOutcome.Unknown);
		var reference = Text(card, "card_id"); var last4 = Text(card, "last4"); var brand = Text(card, "brand");
		var type = brand?.ToUpperInvariant() switch { "VI" or "VISA" => "Visa", "MC" or "MASTERCARD" => "Mastercard", "AE" or "AMEX" => "Amex", _ => null };
		if (!SafeReference(reference) || last4?.Length != 4 || !last4.All(char.IsAsciiDigit)
			|| type is null || !card.TryGetProperty("expiration_month", out var month) || month.ValueKind != JsonValueKind.Number || !month.TryGetInt32(out var m) || m is < 1 or > 12
			|| !card.TryGetProperty("expiration_year", out var year) || year.ValueKind != JsonValueKind.Number || !year.TryGetInt32(out var y) || y is < 2000 or > 9998
			|| new DateTime(y, m, 1).AddMonths(1) <= DateTime.UtcNow) return new(OperatorSetupOutcome.Unknown);
		if (card.TryGetProperty("status", out var status) && (status.ValueKind != JsonValueKind.String || status.GetString() != "ACTIVE")) return new(OperatorSetupOutcome.Unknown);
		return new(OperatorSetupOutcome.Succeeded, new(reference!, last4, type, m, y));
	}
	private static string? Text(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
	private static void ValidateReference(string reference)
	{
		if (!SafeReference(reference))
			throw new InvalidOperationException("Invalid saved-card reference.");
	}
	private static bool SafeReference(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
	private static bool IsDefinitiveCardRejection(HttpStatusCode status) => status is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity;
}
