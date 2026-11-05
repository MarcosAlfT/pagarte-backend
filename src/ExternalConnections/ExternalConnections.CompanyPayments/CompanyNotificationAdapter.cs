using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ExternalConnections.CompanyPayments;

public sealed record CompanyBillNotification(string BillKey, string ServiceCode, string AccountNumber,
    string BillingPeriod, long ServiceAmountMinor, string Currency, string PaymentReference, string ChargeReference);

public sealed record CompanyBillAcknowledgment(string Outcome, CompanyBillNotification Payment,
    string? Reference, string? Reason);

public sealed class CompanyNotificationAdapter(IHttpClientFactory clients)
{
    public const string ClientName = "CompanyNotification";
    private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<CompanyOperationResult> NotifyAsync(string endpoint, string apiKey, string operationKey,
        CompanyBillNotification payment, bool recover, CancellationToken cancellationToken)
    {
        try
        {
            if (recover)
            {
                using var lookup = Request(HttpMethod.Get, endpoint.TrimEnd('/') + "/" + Uri.EscapeDataString(operationKey), apiKey, operationKey);
                using var lookupResponse = await clients.CreateClient(ClientName).SendAsync(lookup, cancellationToken);
                if (lookupResponse.StatusCode != HttpStatusCode.NotFound)
                    return await ReadAcknowledgmentAsync(lookupResponse, payment, cancellationToken);
            }

            using var request = Request(HttpMethod.Post, endpoint, apiKey, operationKey);
            request.Content = JsonContent.Create(payment, options: WireFormat);
            using var response = await clients.CreateClient(ClientName).SendAsync(request, cancellationToken);
            return await ReadAcknowledgmentAsync(response, payment, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
            exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            return Unknown();
        }
    }

    private static async Task<CompanyOperationResult> ReadAcknowledgmentAsync(HttpResponseMessage response,
        CompanyBillNotification expected, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode) return Unknown();
        var acknowledgment = await response.Content.ReadFromJsonAsync<CompanyBillAcknowledgment>(WireFormat, cancellationToken);
        if (acknowledgment?.Payment != expected) return Unknown();
        if (acknowledgment is { Outcome: "accepted", Reference.Length: > 0 and <= 200 } &&
            !string.IsNullOrWhiteSpace(acknowledgment.Reference))
            return new(CompanyOperationOutcome.Succeeded, acknowledgment.Reference, null, null);
        if (acknowledgment is { Outcome: "rejected", Reason: "already_paid" or "bill_changed" or "bill_not_found" })
            return new(CompanyOperationOutcome.DefinitiveFailure, null, "COMPANY_NOTIFICATION_REJECTED",
                "The company rejected confirmation of the charged bill.");
        return Unknown();
    }

    private static HttpRequestMessage Request(HttpMethod method, string endpoint, string apiKey, string operationKey)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Add("X-Api-Key", apiKey);
        request.Headers.Add("Idempotency-Key", operationKey);
        return request;
    }

    private static CompanyOperationResult Unknown() =>
        new(CompanyOperationOutcome.Unknown, null, "COMPANY_ACKNOWLEDGMENT_UNKNOWN", "Company acknowledgment is unconfirmed.");
}
