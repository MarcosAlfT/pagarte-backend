using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ExternalConnections.CompanyPayments;

public interface ICompanyAdapter
{
    Task<CompanyPaymentResult> SendPaymentAsync(
        string apiEndpoint, string apiKey,
        decimal amount, string currency,
        string reference, string clientId);
}

public record CompanyPaymentResult(
    bool Success,
    string? CompanyReference,
    string? ErrorMessage);

public sealed class CompanyAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<CompanyAdapter> logger) : ICompanyAdapter
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<CompanyAdapter> _logger = logger;

    public async Task<CompanyPaymentResult> SendPaymentAsync(
        string apiEndpoint,
        string apiKey,
        decimal amount,
        string currency,
        string reference,
        string clientId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var payload = new { amount, currency, reference, client_id = clientId };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(apiEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Company rejected payment {Reference}: {Error}",
                    reference, error);
                return new CompanyPaymentResult(
                    false,
                    null,
                    $"Company rejected payment: {error}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(
                await response.Content.ReadAsStringAsync());

            return new CompanyPaymentResult(
                true,
                result.GetProperty("reference").GetString(),
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling company for payment {Reference}",
                reference);
            return new CompanyPaymentResult(false, null, ex.Message);
        }
    }
}
