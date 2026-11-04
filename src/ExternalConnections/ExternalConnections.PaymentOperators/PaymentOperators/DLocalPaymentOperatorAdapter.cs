using ExternalConnections.PaymentOperators.Config;
using ExternalConnections.PaymentOperators.PaymentOperators.DLocal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

public sealed class DLocalPaymentOperatorAdapter(
	IHttpClientFactory httpClientFactory,
	IConfiguration configuration,
	ILogger<DLocalPaymentOperatorAdapter> logger, PaymentOperatorRegistry registry, OperatorCardIdentity? identity = null) : IPaymentOperatorAdapter, IStoredCardRemovalAdapter
{
	private readonly DLocalApiClient api = new(httpClientFactory, DLocalConnection.Resolve(configuration, identity));

	public async Task<OperatorCardRemovalOutcome> GetRemovalStatusAsync(OperatorCardIdentity route, string reference, CancellationToken ct)
	{
		if (!GetRemovalCapabilities(route).Supported) return OperatorCardRemovalOutcome.Unsupported;
		var result = await new DLocalCardVaultClient(api).GetCardAsync(reference, ct);
		return result.Outcome switch { OperatorSetupOutcome.NotFound => OperatorCardRemovalOutcome.Removed,
			OperatorSetupOutcome.Succeeded => OperatorCardRemovalOutcome.Pending, _ => OperatorCardRemovalOutcome.Unknown };
	}
	public OperatorCardRemovalCapabilities GetRemovalCapabilities(OperatorCardIdentity route)
	{
		if (identity is null || route.IntegrationId != identity.IntegrationId || route.Provider != identity.Provider
			|| route.Account != identity.Account || route.Environment != identity.Environment || route.Version != identity.Version)
			return new(false, false);
		var settings = registry.GetRequired(identity.IntegrationId);
		return new(settings.CardRemovalEnabled, settings.RefundsSurviveDetachment);
	}
	public Task<OperatorCardRemovalOutcome> RemoveCardAsync(OperatorCardIdentity route, string reference, string key, CancellationToken ct) =>
		GetRemovalCapabilities(route).Supported ? new DLocalCardVaultClient(api).RemoveAsync(reference, key, ct)
			: Task.FromResult(OperatorCardRemovalOutcome.Unsupported);
	public Task<OperatorCardRemovalOutcome> RemoveSetupResourceAsync(OperatorSetupContext context, CancellationToken ct) =>
		Task.FromResult(OperatorCardRemovalOutcome.Unsupported); // No confirmed setup-specific resource ownership contract.

	public string ProviderCode => "DLocal";

	public async Task<CardReadinessResult> CheckCardAsync(
		string cardToken,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var result = await new DLocalCardVaultClient(api).GetCardAsync(cardToken, cancellationToken);
			return result.Outcome switch
			{
				OperatorSetupOutcome.Succeeded => new(PaymentOperatorOutcome.Succeeded, null, null),
				OperatorSetupOutcome.NotFound => new(PaymentOperatorOutcome.DefinitiveDecline, "CARD_NOT_USABLE", "The saved card was not found."),
				_ => new(PaymentOperatorOutcome.Unknown, "CARD_CHECK_UNKNOWN", "The operator could not verify the saved card.")
			};
		}
		catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException)
		{ return new(PaymentOperatorOutcome.Unknown, "CARD_CHECK_UNKNOWN", "The card readiness outcome is unknown."); }
	}

	public async Task<PaymentOperatorChargeResult> ChargeAsync(
		string cardToken,
		decimal amount,
		string currency,
		string countryCode,
		string reference,
		string idempotencyKey,
		CancellationToken cancellationToken = default)
	{
		try
		{
			using var response = await api.SendAsync(
				HttpMethod.Post,
				"/payments",
				new
				{
					amount,
					currency,
					country = countryCode,
					payment_method_id = "CARD",
					payment_method_flow = "DIRECT",
					order_id = reference,
					card = new { card_id = cardToken }
				},
				idempotencyKey,
				cancellationToken);
			return await MapChargeResponseAsync(response, cancellationToken);
		}
		catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
		{
			logger.LogWarning( "dLocal charge outcome is unknown for {Reference}.", reference);
			return new PaymentOperatorChargeResult(
				PaymentOperatorOutcome.Unknown,
				null,
				"CHARGE_UNKNOWN",
				"The operator charge outcome is unknown.");
		}
	}

	public async Task<PaymentOperatorChargeResult> GetChargeStatusAsync(
		string operatorPaymentId,
		CancellationToken cancellationToken = default)
	{
		try
		{
			using var response = await api.SendAsync(
				HttpMethod.Get,
				$"/payments/{Uri.EscapeDataString(operatorPaymentId)}",
				null,
				null,
				cancellationToken);
			var result = await MapChargeResponseAsync(response, cancellationToken);
			return result.OperatorPaymentId is not null && result.OperatorPaymentId != operatorPaymentId
				? new(PaymentOperatorOutcome.Unknown, operatorPaymentId, "CHARGE_REFERENCE_MISMATCH", "The operator returned another payment reference.") : result;
		}
		catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
		{
			logger.LogWarning( "dLocal charge status outcome is unknown.");
			return new PaymentOperatorChargeResult(
				PaymentOperatorOutcome.Unknown,
				operatorPaymentId,
				"CHARGE_STATUS_UNKNOWN",
				"The operator charge status is unknown.");
		}
	}

	public async Task<PaymentOperatorRefundResult> RefundAsync(
		string operatorPaymentId,
		decimal amount,
		string currency,
		string reason,
		string? idempotencyKey = null,
		CancellationToken cancellationToken = default)
	{
		try
		{
			using var response = await api.SendAsync(
				HttpMethod.Post,
				"/refunds",
				new
				{
					payment_id = operatorPaymentId,
					amount,
					currency,
					description = reason
				},
				idempotencyKey,
				cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return new PaymentOperatorRefundResult(
					IsRetryable(response.StatusCode)
						? PaymentOperatorOutcome.RetryableFailure
						: PaymentOperatorOutcome.DefinitiveDecline,
					null,
					"REFUND_FAILED",
					"Refund failed.");
			}

			var result = await DLocalApiClient.ReadJsonAsync(response, cancellationToken);
			var refundId = ReadString(result, "id"); var status = ReadString(result, "status");
			return status == "SUCCESS" && !string.IsNullOrWhiteSpace(refundId)
				? new(PaymentOperatorOutcome.Succeeded, refundId, null, null)
				: new(PaymentOperatorOutcome.Unknown, refundId, "REFUND_UNCONFIRMED", "The operator has not confirmed a successful refund.");
		}
		catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
		{
			logger.LogWarning( "dLocal refund outcome is unknown.");
			return new PaymentOperatorRefundResult(
				PaymentOperatorOutcome.Unknown,
				null,
				"REFUND_UNKNOWN",
				"The refund outcome is unknown.");
		}
	}

	private async Task<PaymentOperatorChargeResult> MapChargeResponseAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (!response.IsSuccessStatusCode)
		{
			return new PaymentOperatorChargeResult(
				IsRetryable(response.StatusCode)
					? PaymentOperatorOutcome.RetryableFailure
					: PaymentOperatorOutcome.DefinitiveDecline,
				null,
				IsRetryable(response.StatusCode) ? "CHARGE_RETRYABLE" : "CARD_DECLINED",
				IsRetryable(response.StatusCode)
					? "The charge failed temporarily."
					: "The card charge was declined.");
		}

		var result = await DLocalApiClient.ReadJsonAsync(response, cancellationToken);
		var paymentId = ReadString(result, "id");
		var status = ReadString(result, "status");
		if (string.Equals(status, "REJECTED", StringComparison.OrdinalIgnoreCase))
		{
			return new PaymentOperatorChargeResult(
				PaymentOperatorOutcome.DefinitiveDecline,
				paymentId,
				"CARD_DECLINED",
				"The card charge was declined.");
		}

		if (string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
		{
			return new PaymentOperatorChargeResult(
				PaymentOperatorOutcome.Unknown,
				paymentId,
				"CHARGE_PROCESSING",
				"The operator charge is still processing.");
		}

		return string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(paymentId)
			? new(PaymentOperatorOutcome.Succeeded, paymentId, null, null)
			: new(PaymentOperatorOutcome.Unknown, paymentId, "CHARGE_STATUS_UNCONFIRMED", "The operator has not confirmed a paid charge.");
	}

	private static string? ReadString(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object
		&& element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

	private static bool IsRetryable(HttpStatusCode statusCode) =>
		statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
		|| (int)statusCode >= 500;

}
