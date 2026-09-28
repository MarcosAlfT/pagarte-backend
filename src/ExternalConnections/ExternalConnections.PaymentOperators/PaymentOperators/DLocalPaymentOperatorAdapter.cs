using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

public sealed class DLocalPaymentOperatorAdapter(
	IHttpClientFactory httpClientFactory,
	IConfiguration configuration,
	ILogger<DLocalPaymentOperatorAdapter> logger) : IPaymentOperatorAdapter
{
	private readonly HttpClient _httpClient = CreateHttpClient(httpClientFactory);
	private readonly string _apiKey = configuration["PaymentOperator:ApiKey"]
		?? throw new InvalidOperationException("PaymentOperator:ApiKey not configured.");
	private readonly string _apiSecret = configuration["PaymentOperator:ApiSecret"]
		?? throw new InvalidOperationException("PaymentOperator:ApiSecret not configured.");

	public string ProviderCode => "DLocal";

	public async Task<PaymentOperatorCardResult> RegisterCardAsync(
		string cardNumber,
		string cvv,
		string cardHolderName,
		int expiryMonth,
		int expiryYear,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var payload = new
			{
				card = new
				{
					number = cardNumber,
					cvv,
					holder_name = cardHolderName,
					expiration_month = expiryMonth,
					expiration_year = expiryYear
				}
			};
			using var response = await SendAsync(
				HttpMethod.Post,
				"/secure_cards",
				payload,
				null,
				cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				logger.LogWarning(
					"dLocal card registration returned HTTP {StatusCode}.",
					(int)response.StatusCode);
				return new PaymentOperatorCardResult(
					false, null, null, null, 0, 0, "Card registration failed.");
			}

			var result = await ReadJsonAsync(response, cancellationToken);
			return new PaymentOperatorCardResult(
				true,
				ReadString(result, "card_id"),
				ReadString(result, "last4"),
				ReadString(result, "brand"),
				result.TryGetProperty("expiration_month", out var month)
					? month.GetInt32()
					: expiryMonth,
				result.TryGetProperty("expiration_year", out var year)
					? year.GetInt32()
					: expiryYear,
				null);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogWarning(exception, "dLocal card registration transport failure.");
			return new PaymentOperatorCardResult(
				false, null, null, null, 0, 0, "Card registration is temporarily unavailable.");
		}
	}

	public async Task<CardReadinessResult> CheckCardAsync(
		string cardToken,
		CancellationToken cancellationToken = default)
	{
		try
		{
			using var response = await SendAsync(
				HttpMethod.Get,
				$"/cards/{Uri.EscapeDataString(cardToken)}",
				null,
				null,
				cancellationToken);
			if (response.IsSuccessStatusCode)
			{
				return new CardReadinessResult(PaymentOperatorOutcome.Succeeded, null, null);
			}

			return IsRetryable(response.StatusCode)
				? new CardReadinessResult(
					PaymentOperatorOutcome.RetryableFailure,
					"CARD_CHECK_RETRYABLE",
					"The operator could not check the card temporarily.")
				: new CardReadinessResult(
					PaymentOperatorOutcome.DefinitiveDecline,
					"CARD_NOT_USABLE",
					"The operator rejected the stored card.");
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogWarning(exception, "dLocal card readiness outcome is unknown.");
			return new CardReadinessResult(
				PaymentOperatorOutcome.Unknown,
				"CARD_CHECK_UNKNOWN",
				"The card readiness outcome is unknown.");
		}
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
			using var response = await SendAsync(
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
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogWarning(exception, "dLocal charge outcome is unknown for {Reference}.", reference);
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
			using var response = await SendAsync(
				HttpMethod.Get,
				$"/payments/{Uri.EscapeDataString(operatorPaymentId)}",
				null,
				null,
				cancellationToken);
			return await MapChargeResponseAsync(response, cancellationToken);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogWarning(exception, "dLocal charge status outcome is unknown.");
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
			using var response = await SendAsync(
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

			var result = await ReadJsonAsync(response, cancellationToken);
			return new PaymentOperatorRefundResult(
				PaymentOperatorOutcome.Succeeded,
				ReadString(result, "id"),
				null,
				null);
		}
		catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
		{
			logger.LogWarning(exception, "dLocal refund outcome is unknown.");
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

		var result = await ReadJsonAsync(response, cancellationToken);
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

		return new PaymentOperatorChargeResult(
			PaymentOperatorOutcome.Succeeded,
			paymentId,
			null,
			null);
	}

	private async Task<HttpResponseMessage> SendAsync(
		HttpMethod method,
		string endpoint,
		object? payload,
		string? idempotencyKey,
		CancellationToken cancellationToken)
	{
		var body = payload is null ? string.Empty : JsonSerializer.Serialize(payload);
		using var request = new HttpRequestMessage(method, endpoint);
		if (payload is not null)
		{
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
		}

		var timestamp = DateTimeOffset.UtcNow.ToString("O");
		var signature = GenerateSignature(timestamp, body);
		request.Headers.Add("X-Date", timestamp);
		request.Headers.Add("X-Login", _apiKey);
		request.Headers.Add("X-Trans-Key", _apiSecret);
		request.Headers.Add("X-Version", "2.1");
		if (!string.IsNullOrWhiteSpace(idempotencyKey))
		{
			request.Headers.Add("X-Idempotency-Key", idempotencyKey);
		}

		request.Headers.TryAddWithoutValidation(
			"Authorization",
			$"V2-HMAC-SHA256, Signature: {signature}");
		return await _httpClient.SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken);
	}

	private string GenerateSignature(string timestamp, string body)
	{
		var message = _apiKey + timestamp + body;
		using var hmac = new System.Security.Cryptography.HMACSHA256(
			Encoding.UTF8.GetBytes(_apiSecret));
		return Convert.ToHexString(
			hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLowerInvariant();
	}

	private static async Task<JsonElement> ReadJsonAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken) =>
		JsonSerializer.Deserialize<JsonElement>(
			await response.Content.ReadAsStringAsync(cancellationToken));

	private static string? ReadString(JsonElement element, string propertyName) =>
		element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;

	private static bool IsRetryable(HttpStatusCode statusCode) =>
		statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
		|| (int)statusCode >= 500;

	private static HttpClient CreateHttpClient(IHttpClientFactory httpClientFactory)
	{
		var httpClient = httpClientFactory.CreateClient("payment-operator");
		return httpClient.BaseAddress is null
			? throw new InvalidOperationException("PaymentOperator:ApiUrl is not configured.")
			: httpClient;
	}
}
