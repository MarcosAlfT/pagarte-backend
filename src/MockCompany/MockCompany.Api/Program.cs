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
        app.MapPost("/payment-notifications", (HttpContext context, MockBillNotification request, MockResponses responses) =>
            responses.Notify(context.Request.Headers["Idempotency-Key"].ToString(), request));
        app.MapGet("/payment-notifications/{operationKey}", (string operationKey, MockResponses responses) =>
            responses.NotificationStatus(operationKey));
        return app;
    }
}

public sealed record MockInquiry(string service_code, Dictionary<string, string> references, DateTime requested_at);
public sealed record MockBill(string BillingPeriod, long AmountMinor, string Currency = "USD");
public sealed record MockBillNotification(string bill_key, string service_code, string account_number, string billing_period,
    long service_amount_minor, string currency, string payment_reference, string charge_reference);

public sealed class MockResponses(TimeProvider clock, IConfiguration? configuration = null)
{
    private readonly ConcurrentDictionary<string, (string Fingerprint, IResult Result)> results = new();
    private readonly string? statePath = configuration?["MockCompany:NotificationStatePath"];
    private MockBillState state = LoadState(configuration?["MockCompany:NotificationStatePath"]);
    private readonly object billLock = new();

    public void SetBill(string service, string account, MockBill bill)
    {
        lock (billLock)
        {
            var bills = new Dictionary<string, MockBill>(state.Bills) { [BillAddress(service, account)] = bill };
            SaveState(state with { Bills = bills });
        }
    }

    public IResult Notify(string operationKey, MockBillNotification payment)
    {
        if (string.IsNullOrWhiteSpace(operationKey) || operationKey.Length > 100 ||
            string.IsNullOrWhiteSpace(payment.payment_reference) || string.IsNullOrWhiteSpace(payment.charge_reference) ||
            string.IsNullOrWhiteSpace(payment.account_number) || string.IsNullOrWhiteSpace(payment.billing_period) ||
            payment.service_amount_minor <= 0)
            return Results.BadRequest();
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new[] { "mock-company", payment.service_code,
            payment.account_number, payment.billing_period });
        if (payment.bill_key != Convert.ToHexStringLower(SHA256.HashData(canonical)))
            return Results.BadRequest();

        lock (billLock)
        {
            if (state.Notifications.TryGetValue(operationKey, out var recorded))
                return recorded.Payment == payment ? Acknowledgment(operationKey, recorded) : Results.Conflict();

            var bill = FindBill(payment.service_code, payment.account_number);
            var reason = bill switch
            {
                null => "bill_not_found",
                _ when bill.BillingPeriod != payment.billing_period || bill.Currency != payment.currency => "bill_changed",
                { AmountMinor: 0 } => "already_paid",
                _ when bill.AmountMinor != payment.service_amount_minor => "bill_changed",
                _ => null
            };
            var notification = new MockRecordedNotification(payment, reason);
            var notifications = new Dictionary<string, MockRecordedNotification>(state.Notifications) { [operationKey] = notification };
            var bills = new Dictionary<string, MockBill>(state.Bills);
            if (reason is null) bills[BillAddress(payment.service_code, payment.account_number)] = bill! with { AmountMinor = 0 };
            SaveState(new(bills, notifications));
            return Acknowledgment(operationKey, notification);
        }
    }

    private static IResult Acknowledgment(string operationKey, MockRecordedNotification notification) =>
        Results.Ok(new
            {
                outcome = notification.Reason is null ? "accepted" : "rejected",
                payment = notification.Payment,
                reference = notification.Reason is null ? "mock-notification-" + operationKey : null,
                reason = notification.Reason
            });

    public IResult NotificationStatus(string operationKey)
    {
        lock (billLock)
            return state.Notifications.TryGetValue(operationKey, out var recorded)
                ? Acknowledgment(operationKey, recorded) : Results.NotFound();
    }

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
        MockBill? bill;
        lock (billLock) bill = FindBill(request.service_code, account);
        if (bill is null) return Results.NotFound();
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

    private MockBill? FindBill(string service, string account)
    {
        if (service is not ("ELEC-EC" or "WATER-EC" or "NET-US")) return null;
        if (state.Bills.TryGetValue(BillAddress(service, account), out var bill)) return bill;
        return account is "10000001" or "10000002" ? new("2026-10", account == "10000001" ? 12845L : 0L) : null;
    }

    private static string BillAddress(string service, string account) => service + "\n" + account;

    private static MockBillState LoadState(string? path) => path is not null && File.Exists(path)
        ? JsonSerializer.Deserialize<MockBillState>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Invalid mock bill state.")
        : new(new(), new());

    private void SaveState(MockBillState updated)
    {
        if (statePath is not null)
        {
            var path = Path.GetFullPath(statePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(updated));
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        state = updated;
    }

    private sealed record MockBillState(Dictionary<string, MockBill> Bills, Dictionary<string, MockRecordedNotification> Notifications);
    private sealed record MockRecordedNotification(MockBillNotification Payment, string? Reason);
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
