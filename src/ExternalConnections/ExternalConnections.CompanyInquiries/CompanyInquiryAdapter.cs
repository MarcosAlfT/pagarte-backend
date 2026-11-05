using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PayableServices.Application.Abstractions;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;

namespace ExternalConnections.CompanyInquiries;

public sealed class CompanyInquiryAdapter(IHttpClientFactory clients, IConfiguration configuration)
    : IAmountInquiryProvider, ICompanyCatalogueProvider, ICompanyRouteConfiguration
{
    private static readonly Meter Meter = new("Pagarte.CompanyInquiryHttp");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("company.inquiry.duration", "ms");
    private static readonly Counter<long> Timeouts = Meter.CreateCounter<long>("company.inquiry.timeouts");
    public bool IsConfigured(string provider) => Settings(provider) is not null;

    public CompanyRouteConnection? Resolve(string provider)
    {
        var settings = Settings(provider);
        return settings is null ? null : new(new Uri(settings.Value.Base, "payments").ToString(),
            new Uri(settings.Value.Base, "payments/rollback").ToString(),
            new Uri(settings.Value.Base, "payments/{reference}").ToString().Replace("%7Breference%7D", "{reference}"), provider);
    }

    public async Task<CompanyCatalogueSnapshot?> GetSnapshotAsync(string source, CancellationToken cancellationToken)
    {
        var settings = Settings(source);
        if (settings is null) return null;
        try
        {
            using var request = Message(settings.Value, HttpMethod.Get,
                configuration[$"CompanyProviders:{source}:CataloguePath"] ?? "catalogue");
            using var response = await clients.CreateClient("CompanyInquiry").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var services = root.GetProperty("services").EnumerateArray().Select(item => new CompanyCatalogueItem(
                item.GetProperty("code").GetString()!, item.GetProperty("name").GetString()!,
                item.GetProperty("description").GetString()!, item.GetProperty("category").GetString()!,
                item.GetProperty("provider").GetString()!, item.GetProperty("country").GetString()!,
                item.GetProperty("currency").GetString()!, item.GetProperty("keywords").GetString()!,
                item.GetProperty("references").EnumerateArray().Select(field => new ReferenceFieldDefinition
                {
                    FieldKey = field.GetProperty("key").GetString()!,
                    DisplayLabel = field.GetProperty("label").GetString()!,
                    DataType = field.GetProperty("type").GetString()!,
                    IsRequired = field.GetProperty("required").GetBoolean(),
                    MinLength = field.GetProperty("minLength").GetInt32(),
                    MaxLength = field.GetProperty("maxLength").GetInt32(),
                    ValidationRule = field.GetProperty("pattern").GetString(),
                    DisplayOrder = field.GetProperty("order").GetInt32()
                }).ToArray())).ToArray();
            return new(root.GetProperty("version").GetInt64(), root.GetProperty("complete").GetBoolean(), services);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return null; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    public async Task<ProviderInquiryResult> InquireAsync(ProviderInquiryRequest inquiry, CancellationToken cancellationToken)
    {
        var settings = Settings(inquiry.Provider);
        if (settings is null) return Failure(InquiryOutcome.TemporarilyUnavailable);
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var request = Message(settings.Value, HttpMethod.Post, "inquiries");
            request.Headers.Add("Idempotency-Key", inquiry.ProviderKey);
            request.Content = JsonContent.Create(new { service_code = inquiry.ExternalServiceCode, references = inquiry.References, requested_at = inquiry.RequestedAt });
            using var response = await clients.CreateClient("CompanyInquiry").SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.RequestTimeout) Timeouts.Add(1);
            if (response.StatusCode == HttpStatusCode.NotFound) return Failure(InquiryOutcome.ReferenceNotFound);
            if (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                return Failure(InquiryOutcome.TemporarilyUnavailable);
            if (!response.IsSuccessStatusCode) return Failure(InquiryOutcome.InvalidProviderResponse);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var outcome = root.GetProperty("outcome").GetString() switch
            {
                "amount_due" => InquiryOutcome.AmountDue,
                "nothing_due" => InquiryOutcome.NothingDue,
                "not_found" => InquiryOutcome.ReferenceNotFound,
                _ => InquiryOutcome.InvalidProviderResponse
            };
            if (outcome is not (InquiryOutcome.AmountDue or InquiryOutcome.NothingDue)) return Failure(outcome);
            var amount = root.GetProperty("amount_minor").GetInt64();
            var currency = root.GetProperty("currency").GetString();
            if (currency is not { Length: 3 } || currency.Any(c => c is < 'A' or > 'Z') ||
                (outcome == InquiryOutcome.AmountDue ? amount <= 0 : amount != 0))
                return Failure(InquiryOutcome.InvalidProviderResponse);
            var references = root.GetProperty("references").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString()!, StringComparer.Ordinal);
            return new(outcome, root.GetProperty("inquiry_id").GetString(), root.GetProperty("service_code").GetString(),
                references, amount, currency, Utc(root.GetProperty("result_at")), Utc(root.GetProperty("expires_at")),
                root.TryGetProperty("due_date", out var due) && due.ValueKind != JsonValueKind.Null ? Utc(due) : null,
                BillingPeriod: root.TryGetProperty("billing_period", out var period) ? period.GetString() : null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { Timeouts.Add(1); return Failure(InquiryOutcome.TemporarilyUnavailable); }
        catch (HttpRequestException) { return Failure(InquiryOutcome.TemporarilyUnavailable); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
        { return Failure(InquiryOutcome.InvalidProviderResponse); }
        finally { Latency.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private static DateTime Utc(JsonElement element)
    {
        var text = element.GetString();
        if (text is null || !text.EndsWith('Z')) throw new FormatException("UTC timestamp required.");
        return element.GetDateTime();
    }
    private static ProviderInquiryResult Failure(InquiryOutcome outcome) => new(outcome);
    private (Uri Base, string Key)? Settings(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider) || provider.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) return null;
        var section = configuration.GetSection($"CompanyProviders:{provider}");
        return section.GetValue<bool>("Enabled") && !string.IsNullOrWhiteSpace(section["ApiKey"]) &&
            Uri.TryCreate(section["BaseUrl"]?.TrimEnd('/') + "/", UriKind.Absolute, out var uri) &&
            (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
            ? (uri, section["ApiKey"]!) : null;
    }
    private static HttpRequestMessage Message((Uri Base, string Key) settings, HttpMethod method, string path)
    {
        var message = new HttpRequestMessage(method, new Uri(settings.Base, path));
        message.Headers.Add("X-Api-Key", settings.Key);
        return message;
    }
}
