using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

public sealed class DLocalPaymentOperatorAdapter(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DLocalPaymentOperatorAdapter> logger) : IPaymentOperatorAdapter
{
    private readonly HttpClient _httpClient =
        CreateHttpClient(httpClientFactory);
    private readonly string _apiKey =
        configuration["PaymentOperator:ApiKey"]
        ?? throw new InvalidOperationException("PaymentOperator:ApiKey not configured.");
    private readonly string _apiSecret =
        configuration["PaymentOperator:ApiSecret"]
        ?? throw new InvalidOperationException("PaymentOperator:ApiSecret not configured.");
    private readonly ILogger<DLocalPaymentOperatorAdapter> _logger = logger;
    public string ProviderCode => "DLocal";

    public async Task<PaymentOperatorCardResult> RegisterCardAsync(
        string cardNumber, string cvv, string cardHolderName,
        int expiryMonth, int expiryYear)
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

            var response = await PostAsync("/secure_cards", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Payment operator card registration failed: {Error}", error);
                return new PaymentOperatorCardResult(false, null, null, null, 0, 0,
                    "Card registration failed.");
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return new PaymentOperatorCardResult(
                true,
                result.GetProperty("card_id").GetString(),
                result.GetProperty("last4").GetString(),
                result.GetProperty("brand").GetString(),
                result.TryGetProperty("expiration_month", out var month)
                    ? month.GetInt32()
                    : expiryMonth,
                result.TryGetProperty("expiration_year", out var year)
                    ? year.GetInt32()
                    : expiryYear,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering card with payment operator");
            return new PaymentOperatorCardResult(false, null, null, null, 0, 0, ex.Message);
        }
    }

    public async Task<PaymentOperatorChargeResult> ChargeAsync(
        string cardToken, decimal amount, string currency, string reference)
    {
        try
        {
            var payload = new
            {
                amount,
                currency,
                payment_method_id = "CARD",
                payment_method_flow = "DIRECT",
                order_id = reference,
                card = new { card_id = cardToken }
            };

            var response = await PostAsync("/payments", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Payment operator charge failed: {Error}", error);
                return new PaymentOperatorChargeResult(false, null, "Payment charge failed.");
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return new PaymentOperatorChargeResult(
                true,
                result.GetProperty("id").GetString(),
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error charging with payment operator");
            return new PaymentOperatorChargeResult(false, null, ex.Message);
        }
    }

    public async Task<PaymentOperatorRefundResult> RefundAsync(
        string operatorPaymentId, decimal amount, string currency, string reason)
    {
        try
        {
            var payload = new
            {
                payment_id = operatorPaymentId,
                amount,
                currency,
                description = reason
            };

            var response = await PostAsync("/refunds", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Payment operator refund failed: {Error}", error);
                return new PaymentOperatorRefundResult(false, null, "Refund failed.");
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return new PaymentOperatorRefundResult(
                true,
                result.GetProperty("id").GetString(),
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding with payment operator");
            return new PaymentOperatorRefundResult(false, null, ex.Message);
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string endpoint, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var signature = GenerateSignature(timestamp, json);

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("X-Date", timestamp);
        _httpClient.DefaultRequestHeaders.Add("X-Login", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("X-Trans-Key", _apiSecret);
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("V2-HMAC-SHA256",
                $"Signature={signature}");

        return await _httpClient.PostAsync(endpoint, content);
    }

    private string GenerateSignature(string timestamp, string body)
    {
        var message = _apiKey + timestamp + body;
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            Encoding.UTF8.GetBytes(_apiSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLower();
    }

    private static HttpClient CreateHttpClient(IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient("payment-operator");
        if (httpClient.BaseAddress == null)
        {
            throw new InvalidOperationException("PaymentOperator:ApiUrl is not configured.");
        }

        return httpClient;
    }
}
