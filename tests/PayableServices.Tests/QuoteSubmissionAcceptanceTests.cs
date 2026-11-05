using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayableServices.Application.Models;
using PayableServices.Domain.Enums;

namespace PayableServices.Tests;

public sealed class QuoteSubmissionAcceptanceTests
{
    [Fact]
    public async Task Pay_atomically_consumes_hides_and_binds_one_card_before_dispatch_then_continues_after_expiry()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var quote = await CreateQuote(rig, client);
        var id = quote.GetProperty("id").GetGuid();
        var card = Guid.NewGuid();
        rig.Clock.UtcNow = quote.GetProperty("expiresAt").GetDateTime().AddSeconds(-1);
        rig.Execution.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Execution.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Pay(client, id, card);
        try
        {
            await rig.Execution.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await rig.InDb(async db =>
            {
                var stored = await db.Quotes.SingleAsync(q => q.Id == id);
                Assert.Equal(QuoteStatus.Submitted, stored.Status);
                Assert.Equal(card, stored.SubmittedCardId);
                Assert.NotNull(stored.SubmissionId);
                Assert.NotNull(stored.SubmissionFingerprint);
                Assert.Equal(stored.SubmittedAt, rig.Execution.Package!.SubmittedAt);
                var evidence = JsonSerializer.Deserialize<BillVerification>(stored.BillVerificationJson!)!;
                Assert.Equal(rig.Execution.Package.BillVerification, evidence);
                Assert.Equal(rig.Execution.Package.BillKey, evidence.ObservedBillKey);
                var inquiry = await db.AmountInquiries.SingleAsync();
                Assert.NotEqual(inquiry.ProviderInquiryId, evidence.ProviderInquiryId);
                Assert.NotEqual(inquiry.ProviderKey, evidence.OperationKey);
                Assert.Null(stored.PaymentId);
                Assert.Equal(1, stored.SubmissionAttemptCount);
            });
            Assert.Empty((await TestRig.Data(await client.GetAsync("/api/quotes"))).GetProperty("quotes").EnumerateArray());
            var pendingStatus = await client.GetAsync($"/api/quotes/{id}/execution");
            Assert.Equal(HttpStatusCode.Accepted, pendingStatus.StatusCode);
            Assert.False((await TestRig.Data(pendingStatus)).GetProperty("final").GetBoolean());
            var duplicates = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Pay(client, id, card)));
            Assert.All(duplicates, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
            Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, Guid.NewGuid())).StatusCode);
            Assert.Equal(1, rig.Execution.ConfirmCalls);
            rig.Clock.UtcNow = quote.GetProperty("expiresAt").GetDateTime().AddSeconds(2);
            Assert.Equal("Submitted", (await TestRig.Data(await client.GetAsync($"/api/quotes/{id}"))).GetProperty("status").GetString());
        }
        finally { rig.Execution.Release.TrySetResult(); }
        var paid = await TestRig.Data(await first);
        Assert.Equal("Completed", paid.GetProperty("status").GetString());
        var again = await TestRig.Data(await Pay(client, id, card));
        Assert.Equal(paid.GetProperty("paymentId").GetGuid(), again.GetProperty("paymentId").GetGuid());
        Assert.Equal(1, rig.Execution.ConfirmCalls);
        await rig.InDb(async db =>
        {
            var stored = await db.Quotes.SingleAsync(q => q.Id == id);
            Assert.Equal(QuoteStatus.Paid, stored.Status);
            Assert.NotNull(stored.SubmissionCompletedAt);
            Assert.Null(stored.SubmissionLeaseId);
            var blocked = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.GetService<IMigrator>().MigrateAsync("20261103045210_AddAmountInquiry"));
            Assert.Equal(51000, blocked.Number);
        });
    }

    [Fact]
    public async Task Exact_deadline_and_foreign_requests_never_claim_or_dispatch_and_expired_quotes_never_reopen()
    {
        await using var rig = await TestRig.CreateAsync();
        using var owner = rig.Client();
        using var foreign = rig.Client(owner: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var quote = await CreateQuote(rig, owner);
        var id = quote.GetProperty("id").GetGuid();
        Assert.Equal(rig.Clock.UtcNow.AddMinutes(5), quote.GetProperty("expiresAt").GetDateTime());
        Assert.Equal(HttpStatusCode.NotFound, (await Pay(foreign, id, Guid.NewGuid())).StatusCode);
        rig.Clock.UtcNow = quote.GetProperty("expiresAt").GetDateTime();
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(owner, id, Guid.NewGuid())).StatusCode);
        rig.Clock.UtcNow = rig.Clock.UtcNow.AddSeconds(-1);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(owner, id, Guid.NewGuid())).StatusCode);
        await rig.InDb(async db =>
        {
            var stored = await db.Quotes.SingleAsync();
            Assert.Equal(QuoteStatus.Expired, stored.Status);
            Assert.Null(stored.SubmissionId);
            Assert.Null(stored.SubmissionPackageJson);
        });
        Assert.Equal(0, rig.Execution.StatusCalls);
        Assert.Equal(0, rig.Execution.ConfirmCalls);
    }

    [Fact]
    public async Task Dispatch_outage_survives_API_restart_and_recovers_the_original_package_after_expiry()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var quote = await CreateQuote(rig, client);
        var id = quote.GetProperty("id").GetGuid();
        var card = Guid.NewGuid();
        rig.Execution.ConfirmUnavailable = true;
        Assert.Equal(HttpStatusCode.Accepted, (await Pay(client, id, card)).StatusCode);
        var package = rig.Execution.Package!;
        rig.Clock.UtcNow = rig.Clock.UtcNow.AddMinutes(10);
        await rig.RestartApiAsync();
        rig.Execution.ConfirmUnavailable = false;
        await rig.RecoverQuote(id);
        var recovered = rig.Execution.Package!;
        Assert.Equal(JsonSerializer.Serialize(package with { BillVerification = null }),
            JsonSerializer.Serialize(recovered with { BillVerification = null }));
        Assert.NotEqual(package.BillVerification!.OperationKey, recovered.BillVerification!.OperationKey);
        Assert.Equal(rig.Clock.UtcNow, recovered.BillVerification.CheckedAt);
        Assert.Single(rig.Execution.Results);
        using var restored = rig.Client();
        Assert.Equal("Paid", (await TestRig.Data(await restored.GetAsync($"/api/quotes/{id}"))).GetProperty("status").GetString());
        Assert.Empty((await TestRig.Data(await restored.GetAsync("/api/quotes"))).GetProperty("quotes").EnumerateArray());
        await rig.InDb(async db => Assert.Equal(2, (await db.Quotes.SingleAsync()).SubmissionAttemptCount));
    }

    [Theory]
    [InlineData("2026-11", 12845L, "USD", "BILL_CHANGED", QuoteStatus.Invalidated)]
    [InlineData("2026-10", 12900L, "USD", "BILL_CHANGED", QuoteStatus.Invalidated)]
    [InlineData("2026-10", 12845L, "EUR", "BILL_CHANGED", QuoteStatus.Invalidated)]
    [InlineData("2026-10", 0L, "USD", "BILL_NO_LONGER_DUE", QuoteStatus.NoLongerDue)]
    [InlineData("invalid", 12845L, "USD", "BILL_VERIFICATION_UNAVAILABLE", QuoteStatus.PaymentFailed)]
    public async Task Verification_closes_a_consumed_quote_when_the_bill_cannot_be_paid(
        string period, long amount, string currency, string code, QuoteStatus status)
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var id = (await CreateQuote(rig, client)).GetProperty("id").GetGuid();
        rig.SetBill("10000001", period, amount, currency);
        var card = Guid.NewGuid();
        var failed = await Pay(client, id, card);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Equal(code, (await failed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("errorCode").GetString());
        rig.SetBill("10000001", "2026-10", 12845);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, card)).StatusCode);
        Assert.Equal(2, rig.Provider.Keys.Distinct().Count());
        Assert.Single(rig.Execution.Results);
        await rig.InDb(async db =>
        {
            var stored = await db.Quotes.SingleAsync();
            Assert.Equal(status, stored.Status);
            Assert.Null(stored.PaymentId);
            Assert.NotNull(stored.SubmissionCompletedAt);
        });
    }

    [Fact]
    public async Task Disabled_original_route_prevents_verification_and_payment()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var id = (await CreateQuote(rig, client)).GetProperty("id").GetGuid();
        await rig.InDb(async db =>
        {
            (await db.PaymentRoutes.SingleAsync(route => route.Id == rig.RouteId)).IsActive = false;
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, Guid.NewGuid())).StatusCode);
        Assert.Equal("BILL_ROUTE_UNAVAILABLE", rig.Execution.Package!.BillVerification!.ErrorCode);
        Assert.Equal(1, rig.Provider.Calls);
    }

    [Fact]
    public async Task Bill_identity_preserves_leading_zeroes_and_ignores_customer_and_inquiry_ids()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        using var other = rig.Client(owner: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        rig.SetBill("010000001", "2026-10", 12845);
        var first = (await CreateQuote(rig, client, "010000001")).GetProperty("id").GetGuid();
        var same = (await CreateQuote(rig, other, "010000001")).GetProperty("id").GetGuid();
        var different = (await CreateQuote(rig, client)).GetProperty("id").GetGuid();
        await rig.InDb(async db =>
        {
            var quotes = await db.Quotes.ToDictionaryAsync(quote => quote.Id);
            var bill = JsonSerializer.Deserialize<BillIdentity>(quotes[first].BillIdentityJson!)!;
            Assert.Equal("010000001", bill.AccountNumber);
            Assert.Equal("2026-10", bill.BillingPeriod);
            Assert.Equal(quotes[first].BillIdentityJson, quotes[same].BillIdentityJson);
            Assert.NotEqual(bill.Key, JsonSerializer.Deserialize<BillIdentity>(quotes[different].BillIdentityJson!)!.Key);
        });
    }

    [Fact]
    public async Task Inquiry_without_supported_identity_cannot_create_a_quote()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        rig.SetBill("10000001", "invalid", 12845);
        var inquiry = await TestRig.Data(await rig.Inquire(client));
        Assert.Equal(HttpStatusCode.BadRequest, (await rig.Quote(client, inquiry.GetProperty("id").GetGuid())).StatusCode);
    }

    [Fact]
    public async Task Migration_invalidates_unsubmitted_inquiry_quotes_without_inventing_bill_identity()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var id = (await CreateQuote(rig, client)).GetProperty("id").GetGuid();
        await rig.InDb(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261105044041_AddSingleUseQuoteSubmission");
            await migrator.MigrateAsync();
            var quote = await db.Quotes.SingleAsync();
            Assert.Equal(QuoteStatus.Invalidated, quote.Status);
            Assert.Equal("BILL_IDENTITY_REQUIRED", quote.ExecutionErrorCode);
            Assert.Null(quote.BillIdentityJson);
            Assert.Null(quote.SubmissionId);
            Assert.NotNull(quote.InquiryId);
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, Guid.NewGuid())).StatusCode);
        Assert.Equal(0, rig.Execution.ConfirmCalls);
    }

    [Fact]
    public async Task Accepted_payment_requesting_fresh_evidence_is_recovered_with_the_same_submission()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var id = (await CreateQuote(rig, client)).GetProperty("id").GetGuid();
        var card = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var processing = new ConfirmQuoteResult(true, paymentId, "execution-reference", QuoteExecutionStatus.Processing,
            "BILL_VERIFICATION_STALE", "Verify again.", false, false, true);
        rig.Execution.ConfirmOverride = (_, _) =>
        {
            rig.Execution.Results[(TestRig.Owner, id)] = processing;
            return Task.FromResult(processing);
        };
        Assert.Equal(HttpStatusCode.Accepted, (await Pay(client, id, card)).StatusCode);
        var original = rig.Execution.Package!;
        rig.Clock.UtcNow = rig.Clock.UtcNow.AddMinutes(2);
        rig.Execution.ConfirmOverride = (_, _) => Task.FromResult(processing with
        {
            Status = QuoteExecutionStatus.Completed, ErrorCode = null, ErrorMessage = null, Final = true
        });
        await rig.RecoverQuote(id);
        var refreshed = rig.Execution.Package!;
        Assert.Equal(original.SubmissionId, refreshed.SubmissionId);
        Assert.Equal(original.BillKey, refreshed.BillKey);
        Assert.NotEqual(original.BillVerification!.OperationKey, refreshed.BillVerification!.OperationKey);
        await rig.InDb(async db =>
        {
            var quote = await db.Quotes.SingleAsync();
            Assert.Equal(paymentId, quote.PaymentId);
            Assert.Equal(QuoteStatus.Paid, quote.Status);
        });
    }

    [Fact]
    public async Task A_durable_rejection_permanently_closes_the_quote_even_if_the_card_later_becomes_available()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var quote = await CreateQuote(rig, client);
        var id = quote.GetProperty("id").GetGuid();
        var card = Guid.NewGuid();
        rig.Execution.Results[(TestRig.Owner, id)] = new(false, null, null, QuoteExecutionStatus.Failed,
            "CARD_UNAVAILABLE", "Stored card unavailable.", false, true, true);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, card)).StatusCode);
        rig.Execution.Results[(TestRig.Owner, id)] = new(true, Guid.NewGuid(), "late-card", QuoteExecutionStatus.Completed,
            null, null, false, true, true);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, card)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, Guid.NewGuid())).StatusCode);
        await rig.InDb(async db =>
        {
            var stored = await db.Quotes.SingleAsync();
            Assert.Equal(QuoteStatus.PaymentFailed, stored.Status);
            Assert.Null(stored.PaymentId);
            Assert.NotNull(stored.SubmittedAt);
            Assert.NotNull(stored.SubmissionCompletedAt);
        });
        Assert.Equal(1, rig.Execution.ConfirmCalls);
        Assert.Empty((await TestRig.Data(await client.GetAsync("/api/quotes"))).GetProperty("quotes").EnumerateArray());
    }

    [Fact]
    public async Task An_expired_worker_lease_cannot_regress_the_final_result_saved_by_recovery()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var quote = await CreateQuote(rig, client);
        var id = quote.GetProperty("id").GetGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paymentId = Guid.NewGuid();
        var calls = 0;
        rig.Execution.ConfirmOverride = async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(ct);
                return new(true, paymentId, "old-response", QuoteExecutionStatus.Processing, null, null, false, false, true);
            }
            var completed = new ConfirmQuoteResult(true, paymentId, "completed", QuoteExecutionStatus.Completed, null, null, false, true, true);
            rig.Execution.Results[(TestRig.Owner, id)] = completed;
            return completed;
        };
        var first = Pay(client, id, Guid.NewGuid());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            rig.Clock.UtcNow = rig.Clock.UtcNow.AddMinutes(2);
            await rig.RecoverQuote(id);
        }
        finally { release.TrySetResult(); }
        Assert.Equal("Completed", (await TestRig.Data(await first)).GetProperty("status").GetString());
        await rig.InDb(async db => Assert.Equal(QuoteStatus.Paid, (await db.Quotes.SingleAsync()).Status));
        Assert.Equal(2, rig.Execution.ConfirmCalls);
    }

    [Fact]
    public async Task Failed_submission_commit_never_dispatches_external_work()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var id = (await CreateQuote(rig, client)).GetProperty("id").GetGuid();
        rig.Faults.FailSubmissionCommit = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Pay(client, id, Guid.NewGuid())).StatusCode);
        Assert.Equal(0, rig.Execution.ConfirmCalls);
        Assert.Equal(0, rig.Execution.StatusCalls);
        await rig.InDb(async db =>
        {
            var quote = await db.Quotes.SingleAsync();
            Assert.Null(quote.SubmissionId);
            Assert.Equal(QuoteStatus.Unpaid, quote.Status);
        });
    }

    [Fact]
    public async Task Migration_preserves_legacy_snapshots_and_binding_without_fabricating_submission_evidence()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var quote = await CreateQuote(rig, client);
        var id = quote.GetProperty("id").GetGuid();
        await rig.InDb(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261103045210_AddAmountInquiry");
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var preserved = await db.Quotes.Include(q => q.Items).SingleAsync(q => q.Id == id);
            Assert.Equal(QuoteStatus.Expired, preserved.Status);
            Assert.Equal(0, preserved.LifecycleVersion);
            Assert.Null(preserved.SubmittedAt);
            Assert.Null(preserved.SubmissionId);
            Assert.Equal(quote.GetProperty("inquiryId").GetGuid(), preserved.InquiryId);
            Assert.Equal(quote.GetProperty("totalMinorUnits").GetInt64(), preserved.TotalMinorUnits);
            Assert.Equal(id, (await db.AmountInquiries.SingleAsync()).QuoteId);
            Assert.NotEmpty(preserved.Items);
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await Pay(client, id, Guid.NewGuid())).StatusCode);
        Assert.Equal(0, rig.Execution.ConfirmCalls);
    }

    private static Task<HttpResponseMessage> Pay(HttpClient client, Guid quote, Guid card) =>
        client.PostAsJsonAsync($"/api/quotes/{quote}/confirm", new { creditCardId = card });

    private static async Task<JsonElement> CreateQuote(TestRig rig, HttpClient client, string account = "10000001")
    {
        var inquiry = await TestRig.Data(await rig.Inquire(client, account));
        return await TestRig.Data(await rig.Quote(client, inquiry.GetProperty("id").GetGuid()));
    }
}
