using System.Net;
using System.Net.Http.Json;
using ExternalConnections.CompanyPayments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Infrastructure;

namespace PayableServices.Tests;

public sealed class PaymentRecoveryEndToEndTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Public_pay_recovers_original_outcome_after_lost_grpc_response_and_restart(bool useOwnedCard, bool externalPayment)
    {
        await using var quotes = await TestRig.CreateAsync();
        await using var processor = new ProcessorHost(quotes);
        if (externalPayment) processor.BeforeNotification = () => quotes.SetBill("10000001", "2026-10", 0);
        await processor.StartAsync();
        quotes.Settings["PaymentSwitchProcessor:GrpcUrl"] = "http://localhost";
        quotes.Settings["WorkloadAuth:ApiKey"] = ProcessorHost.WorkloadKey;
        quotes.ExecutionHandler = processor.CreateExecutionHandler;
        await quotes.RestartApiAsync();
        var card = useOwnedCard ? await processor.SeedCardAsync() : Guid.NewGuid();
        using var customer = quotes.Client();
        await SyntheticQuotePricing.StoreAsync(quotes, SyntheticQuotePricing.Definition());
        var inquiry = await TestRig.Data(await quotes.Inquire(customer));
        var quote = await TestRig.Data(await quotes.Quote(customer, inquiry.GetProperty("id").GetGuid()));
        Assert.Equal(SyntheticQuotePricing.Total, quote.GetProperty("totalMinorUnits").GetInt64());
        await SyntheticQuotePricing.StoreAsync(quotes, SyntheticQuotePricing.ChangedAllocation());
        var id = quote.GetProperty("id").GetGuid();
        var confirm = $"/api/quotes/{id}/confirm";
        Assert.Equal(HttpStatusCode.Accepted,
            (await customer.PostAsJsonAsync(confirm, new { creditCardId = card })).StatusCode);
        Assert.Equal(1, processor.SubmitCalls);

        await processor.StartAsync();
        await quotes.RestartApiAsync();
        using var restored = quotes.Client();
        using var foreign = quotes.Client(owner: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/quotes/{id}/execution")).StatusCode);
        var expected = useOwnedCard ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
        var result = await restored.GetAsync($"/api/quotes/{id}/execution");
        Assert.Equal(expected, result.StatusCode);
        if (useOwnedCard)
        {
            var execution = await TestRig.Data(result);
            Assert.Equal(externalPayment ? "Refunded" : "Completed", execution.GetProperty("status").GetString());
        }
        else
        {
            var failure = await result.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("CARD_NOT_OWNED", failure.GetProperty("data").GetProperty("errorCode").GetString());
        }
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => restored.PostAsJsonAsync(confirm, new { creditCardId = card })));
        Assert.All(duplicates, response => Assert.Equal(expected, response.StatusCode));
        Assert.Equal(1, processor.SubmitCalls);
        var detail = await TestRig.Data(await restored.GetAsync($"/api/quotes/{id}"));
        Assert.Equal(externalPayment ? "Refunded" : useOwnedCard ? "Paid" : "PaymentFailed", detail.GetProperty("status").GetString());
        Assert.Empty((await TestRig.Data(await restored.GetAsync("/api/quotes"))).GetProperty("quotes").EnumerateArray());
        await processor.VerifyAsync(useOwnedCard, externalPayment);
    }

    private sealed class ProcessorHost(TestRig quotes) : IAsyncDisposable
    {
        public const string WorkloadKey = "synthetic-end-to-end-pay-workload";
        private readonly string database = "PagarteQuoteRecoveryTests_" + Guid.NewGuid().ToString("N");
        private readonly string journal = Path.Combine(Path.GetTempPath(), "pagarte-quote-recovery-" + Guid.NewGuid().ToString("N"));
        private WebApplication? app;
        private int submitCalls;
        public int SubmitCalls => submitCalls;
        public Action? BeforeNotification { get; set; }

        private string Connection => new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PAGARTE_TEST_SQL") ??
            @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true") { InitialCatalog = database }.ConnectionString;

        public async Task StartAsync()
        {
            if (app is not null) await app.DisposeAsync();
            app = await PaymentSwitch.Processor.Program.BuildAsync([
                "--environment", "Development", "--ConnectionStrings:PaymentDb", Connection, "--PaymentOperator:Provider", "Mock",
                "--WorkloadAuth:ApiKey", WorkloadKey, "--ChargeFirst:Enabled", "true",
                "--ChargeFirst:MockCompanyBaseUrl", "http://localhost/", "--CompanyCredentials:MockCompany:ApiKey", "synthetic-test-key",
                "--PaymentOperators:MockStateDirectory", journal, "--PaymentOperators:DefaultIntegration", "recovery-mock",
                "--PaymentOperators:Integrations:recovery-mock:Adapter", "Mock",
                "--PaymentOperators:Integrations:recovery-mock:Account", "synthetic-recovery",
                "--PaymentOperators:Integrations:recovery-mock:Environment", "Development",
                "--PaymentOperators:Integrations:recovery-mock:Version", "1",
                "--PaymentOperators:Integrations:recovery-mock:SupportedCountries:0", "EC",
                "--PaymentOperators:Integrations:recovery-mock:SupportedCurrencies:0", "USD"
            ], builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Services.RemoveAll<IHostedService>();
                builder.Services.AddSingleton<PaymentSwitch.Messaging.IClock>(quotes.Clock);
                builder.Services.AddHttpClient(CompanyNotificationAdapter.ClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new CompanyHandler(quotes.CreateCompanyHandler(), this));
            });
            await app.StartAsync();
        }

        public HttpMessageHandler CreateExecutionHandler() => new LostResponseHandler(app!.GetTestServer().CreateHandler(), this);

        public async Task<Guid> SeedCardAsync()
        {
            using var scope = app!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var card = CreditCard.Create(TestRig.Owner, "recovery-mock", "synthetic-card-reference", "Test Card",
                "4242", CardType.Visa, 12, 2040, false, quotes.Clock.UtcNow);
            db.CreditCards.Add(card);
            await db.SaveChangesAsync();
            return card.Id;
        }

        public async Task VerifyAsync(bool charged, bool refunded)
        {
            using var scope = app!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var outcome = await db.QuoteSubmissionOutcomes.SingleAsync();
            if (charged)
            {
                var payment = await db.Payments.Include(row => row.Details).SingleAsync();
                Assert.Equal(payment.Id, outcome.PaymentId);
                Assert.True(payment.ChargeFirst);
                Assert.Equal(1, payment.ChargeAttempts);
                Assert.Equal(1, payment.CompanyPaymentAttempts);
                Assert.Equal(refunded ? 1 : 0, payment.CompensationAttempts);
                Assert.Equal(SyntheticQuotePricing.Total, payment.TotalMinorUnits);
                Assert.Equal(new long[] { 12845, 200, 1284, 716, 6, -25, -148 },
                    payment.Details.OrderBy(detail => detail.DisplayOrder).Select(detail => detail.MinorUnits).ToArray());
                Assert.Equal(refunded ? 2 : 1, Directory.EnumerateFiles(journal, "*.payment.json").Count());
            }
            else
            {
                Assert.Empty(await db.Payments.ToArrayAsync());
                Assert.Equal("CARD_NOT_OWNED", outcome.ErrorCode);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (app is not null) await app.DisposeAsync();
            await using var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>().UseSqlServer(Connection).Options);
            await db.Database.EnsureDeletedAsync();
            var resolved = Path.GetFullPath(journal);
            if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(resolved).StartsWith("pagarte-quote-recovery-", StringComparison.Ordinal) && Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }

        private sealed class CompanyHandler(HttpMessageHandler inner, ProcessorHost host) : DelegatingHandler(inner)
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Method == HttpMethod.Post) host.BeforeNotification?.Invoke();
                return base.SendAsync(request, cancellationToken);
            }
        }

        private sealed class LostResponseHandler(HttpMessageHandler inner, ProcessorHost host) : DelegatingHandler(inner)
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (!request.RequestUri!.AbsolutePath.EndsWith("/SubmitVerifiedBill", StringComparison.Ordinal) ||
                    Interlocked.Increment(ref host.submitCalls) != 1)
                    return response;

                using (response) await response.Content.CopyToAsync(Stream.Null, cancellationToken);
                throw new HttpRequestException("Synthetic lost gRPC submission response.");
            }
        }
    }
}
