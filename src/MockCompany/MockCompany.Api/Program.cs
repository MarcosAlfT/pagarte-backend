using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MockCompany.Api;

public static class Program
{
    public static void Main(string[] args) => Build(args).Run();
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Mock company requires Development.");
        if (string.IsNullOrWhiteSpace(builder.Configuration["MockCompany:ApiKey"]))
            throw new InvalidOperationException("MockCompany:ApiKey is required.");
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<MockResponses>();
        configure?.Invoke(builder);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers["X-Api-Key"] != app.Configuration["MockCompany:ApiKey"])
            { context.Response.StatusCode = 401; return; }
            await next(context);
        });
        app.MapGet("/catalogue", (string? scenario) => MockCatalogue.Feed(scenario ?? "initial"));
        app.MapPost("/inquiries", (HttpContext context, MockInquiry request, MockResponses responses) =>
            responses.Inquire(context.Request.Headers["Idempotency-Key"].ToString(), request));
        app.MapPost("/payments", (HttpContext context) => Results.Ok(new { reference = "mock-payment-" + context.Request.Headers["Idempotency-Key"].ToString(), status = "SUCCEEDED" }));
        app.MapPost("/payments/rollback", () => Results.Ok(new { status = "SUCCEEDED" }));
        app.MapGet("/payments/{reference}", (string reference) => Results.Ok(new { reference, status = "SUCCEEDED" }));
        return app;
    }
}

public sealed record MockInquiry(string service_code, Dictionary<string, string> references, DateTime requested_at);
public sealed record MockBill(string BillingPeriod, long AmountMinor, string Currency = "USD");

public sealed class MockResponses(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (string Fingerprint, IResult Result)> results = new();
    private readonly ConcurrentDictionary<(string Service, string Account), MockBill> bills = new();

    public void SetBill(string service, string account, MockBill bill) => bills[(service, account)] = bill;

    public IResult Inquire(string key, MockInquiry request)
    {
        if (string.IsNullOrWhiteSpace(key) || request.references is null || !request.references.TryGetValue("accountNumber", out var account))
            return Results.BadRequest();
        var fingerprint = JsonSerializer.Serialize(request);
        var stored = results.GetOrAdd(key, _ => (fingerprint, Create(key, request, account)));
        return stored.Fingerprint == fingerprint ? stored.Result : Results.Conflict();
    }
    private IResult Create(string key, MockInquiry request, string account)
    {
        if (request.service_code is not ("ELEC-EC" or "WATER-EC" or "NET-US")) return Results.NotFound();
        if (account == "10000004") return Results.StatusCode(503);
        // A deterministic provider-side timeout response; tests also simulate a transport cancellation.
        if (account == "10000005") return Results.StatusCode(408);
        if (account == "10000006") return Results.Ok(new { outcome = "amount_due", amount_minor = "bad", currency = "EUR" });
        if (!bills.TryGetValue((request.service_code, account), out var bill))
        {
            if (account is not ("10000001" or "10000002")) return Results.NotFound();
            bill = new("2026-10", account == "10000001" ? 12845L : 0L);
        }
        var now = request.requested_at;
        if (now.Kind != DateTimeKind.Utc || now > clock.GetUtcNow().UtcDateTime || now > DateTime.MaxValue.AddDays(-8))
            return Results.BadRequest();
        return Results.Ok(new
        {
            outcome = bill.AmountMinor > 0 ? "amount_due" : "nothing_due",
            inquiry_id = "mock-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))),
            service_code = request.service_code,
            references = request.references,
            billing_period = bill.BillingPeriod,
            amount_minor = bill.AmountMinor,
            currency = bill.Currency,
            result_at = now,
            expires_at = now.AddMinutes(15),
            due_date = now.Date.AddDays(7)
        });
    }
}

public static class MockCatalogue
{
    public static IResult Feed(string scenario)
    {
        if (scenario == "unavailable") return Results.StatusCode(503);
        if (scenario == "malformed") return Results.Text("{invalid-json", "application/json");
        var services = new List<object> {
            Item("ELEC-EC", scenario is "updated" or "same-version" ? "Electricity Quito Updated" : "Electricity Quito", "Utilities", "Quito Electricity", "EC",
                scenario == "wrongCurrency" ? "EUR" : "USD", scenario == "invalidReferences" ? 0 : 20),
            Item("WATER-EC", "Water Quito", "Utilities", "Quito Water", "EC"),
            Item("NET-US", "Internet USA", "Telecom", "Example Internet", "US") };
        if (scenario == "removed") services.RemoveAt(1);
        if (scenario == "invalid") services.Add(services[0]);
        return Results.Ok(new
        {
            version = scenario switch { "updated" => 2L, "removed" => 3L, "invalid" => 4L, _ => 1L },
            complete = scenario != "partial",
            services
        });
    }
    private static object Item(string code, string name, string category, string provider, string country, string currency = "USD", int maxLength = 20) => new
    {
        code,
        name,
        description = "Synthetic " + name + " billing service",
        category,
        provider,
        country,
        currency,
        keywords = "bill power utility internet",
        references = new[] { new { key = "accountNumber", label = "Account Number", type = "digits", required = true,
            minLength = 8, maxLength, pattern = "^[0-9]{8,20}$", order = 1 } }
    };
}
