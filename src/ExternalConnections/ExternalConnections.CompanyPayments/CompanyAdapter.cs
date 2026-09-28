using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;
using Utilities.Money;

namespace ExternalConnections.CompanyPayments;

public enum CompanyOperationOutcome
{
	Succeeded,
	DefinitiveFailure,
	RetryableFailure,
	Unknown
}

public sealed record CompanyPaymentRequest(
	string ApiEndpoint,
	string ApiKey,
	string ExternalServiceCode,
	IReadOnlyDictionary<string, string> ReferenceValues,
	MoneyValue Amount,
	string PaymentReference,
	string ClientId,
	string IdempotencyKey);

public sealed record CompanyRollbackRequest(
	string ApiEndpoint,
	string ApiKey,
	string ExternalServiceCode,
	string CompanyReference,
	MoneyValue Amount,
	string PaymentReference,
	string Reason,
	string IdempotencyKey);

public sealed record CompanyStatusRequest(
	string ApiEndpoint,
	string ApiKey,
	string CompanyReference,
	string IdempotencyKey);

public sealed record CompanyOperationResult(
	CompanyOperationOutcome Outcome,
	string? CompanyReference,
	string? ErrorCode,
	string? ErrorMessage)
{
	public bool Success => Outcome == CompanyOperationOutcome.Succeeded;
}

public interface ICompanyAdapter
{
	Task<CompanyOperationResult> PayServiceAsync(
		CompanyPaymentRequest request,
		CancellationToken cancellationToken = default);

	Task<CompanyOperationResult> RollbackServicePaymentAsync(
		CompanyRollbackRequest request,
		CancellationToken cancellationToken = default);

	Task<CompanyOperationResult> GetOperationStatusAsync(
		CompanyStatusRequest request,
		CancellationToken cancellationToken = default);
}

public sealed class CompanyAdapter(
	IHttpClientFactory httpClientFactory,
	ILogger<CompanyAdapter> logger) : ICompanyAdapter
{
	public Task<CompanyOperationResult> PayServiceAsync(
		CompanyPaymentRequest request,
		CancellationToken cancellationToken = default) =>
		PostAsync(
			request.ApiEndpoint,
			request.ApiKey,
			request.IdempotencyKey,
			new
			{
				service_code = request.ExternalServiceCode,
				references = request.ReferenceValues,
				amount = request.Amount.Amount,
				currency = request.Amount.Currency,
				reference = request.PaymentReference,
				client_id = request.ClientId
			},
			"company payment",
			cancellationToken);

	public Task<CompanyOperationResult> RollbackServicePaymentAsync(
		CompanyRollbackRequest request,
		CancellationToken cancellationToken = default) =>
		PostAsync(
			request.ApiEndpoint,
			request.ApiKey,
			request.IdempotencyKey,
			new
			{
				service_code = request.ExternalServiceCode,
				company_reference = request.CompanyReference,
				amount = request.Amount.Amount,
				currency = request.Amount.Currency,
				reference = request.PaymentReference,
				reason = request.Reason
			},
			"company rollback",
			cancellationToken);

	public async Task<CompanyOperationResult> GetOperationStatusAsync(
		CompanyStatusRequest request,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.ApiEndpoint))
		{
			return new CompanyOperationResult(
				CompanyOperationOutcome.Unknown,
				request.CompanyReference,
				"COMPANY_STATUS_UNAVAILABLE",
				"The payment route does not provide a company status endpoint.");
		}

		try
		{
			var endpoint = request.ApiEndpoint.Replace(
				"{reference}",
				Uri.EscapeDataString(request.CompanyReference),
				StringComparison.Ordinal);
			using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
			AddHeaders(message, request.ApiKey, request.IdempotencyKey);
			using var response = await httpClientFactory.CreateClient().SendAsync(
				message,
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken);
			return await MapResponseAsync(response, "company status", cancellationToken);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return Unknown("COMPANY_TIMEOUT", "The company status request timed out.");
		}
		catch (HttpRequestException exception)
		{
			logger.LogWarning(exception, "Company status transport failure.");
			return Unknown("COMPANY_TRANSPORT_ERROR", "The company status outcome is unknown.");
		}
	}

	private async Task<CompanyOperationResult> PostAsync(
		string endpoint,
		string apiKey,
		string idempotencyKey,
		object payload,
		string operation,
		CancellationToken cancellationToken)
	{
		try
		{
			var json = JsonSerializer.Serialize(payload);
			using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			};
			AddHeaders(message, apiKey, idempotencyKey);
			using var response = await httpClientFactory.CreateClient().SendAsync(
				message,
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken);
			return await MapResponseAsync(response, operation, cancellationToken);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return Unknown("COMPANY_TIMEOUT", $"The {operation} outcome is unknown after a timeout.");
		}
		catch (HttpRequestException exception)
		{
			logger.LogWarning(exception, "Transport failure during {Operation}.", operation);
			return Unknown("COMPANY_TRANSPORT_ERROR", $"The {operation} outcome is unknown.");
		}
	}

	private async Task<CompanyOperationResult> MapResponseAsync(
		HttpResponseMessage response,
		string operation,
		CancellationToken cancellationToken)
	{
		var body = await response.Content.ReadAsStringAsync(cancellationToken);
		if (response.IsSuccessStatusCode)
		{
			var reference = TryReadString(body, "reference");
			var status = TryReadString(body, "status");
			if (string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(status, "PROCESSING", StringComparison.OrdinalIgnoreCase))
			{
				return Unknown("COMPANY_PROCESSING", $"The {operation} is still processing.", reference);
			}

			if (string.Equals(status, "REJECTED", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
			{
				return new CompanyOperationResult(
					CompanyOperationOutcome.DefinitiveFailure,
					reference,
					"COMPANY_REJECTED",
					$"The company rejected the {operation}.");
			}

			return new CompanyOperationResult(
				CompanyOperationOutcome.Succeeded,
				reference,
				null,
				null);
		}

		logger.LogWarning(
			"Company {Operation} returned HTTP {StatusCode}.",
			operation,
			(int)response.StatusCode);
		var retryable = response.StatusCode is HttpStatusCode.RequestTimeout
			or HttpStatusCode.TooManyRequests
			|| (int)response.StatusCode >= 500;
		return new CompanyOperationResult(
			retryable
				? CompanyOperationOutcome.RetryableFailure
				: CompanyOperationOutcome.DefinitiveFailure,
			null,
			retryable ? "COMPANY_RETRYABLE" : "COMPANY_REJECTED",
			retryable
				? $"The {operation} failed temporarily."
				: $"The company rejected the {operation}.");
	}

	private static void AddHeaders(
		HttpRequestMessage message,
		string apiKey,
		string idempotencyKey)
	{
		message.Headers.Add("X-Api-Key", apiKey);
		message.Headers.Add("Idempotency-Key", idempotencyKey);
	}

	private static string? TryReadString(string json, string propertyName)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}

		try
		{
			var element = JsonSerializer.Deserialize<JsonElement>(json);
			return element.TryGetProperty(propertyName, out var property)
				? property.GetString()
				: null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static CompanyOperationResult Unknown(
		string code,
		string message,
		string? reference = null) =>
		new(CompanyOperationOutcome.Unknown, reference, code, message);
}
