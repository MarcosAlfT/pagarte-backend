using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Models;
using PayableServices.Domain.Entities;
using PayableServices.Domain.Enums;

namespace PayableServices.Tests;

public sealed class QuoteHistoryAcceptanceTests
{
    private const string OtherOwner = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    [Theory]
    [InlineData(null, true, HttpStatusCode.Unauthorized)]
    [InlineData("support", true, HttpStatusCode.Forbidden)]
    [InlineData("client", false, HttpStatusCode.Forbidden)]
    [InlineData("client", true, HttpStatusCode.OK)]
    public async Task History_requires_a_verified_client(string? role, bool verified, HttpStatusCode expected)
    {
        await using var rig = await TestRig.CreateAsync(activate: false);
        using var client = rig.Client(role, verified: verified);
        Assert.Equal(expected, (await client.GetAsync("/api/quotes")).StatusCode);
    }

    [Fact]
    public async Task History_is_owner_scoped_bounded_and_reads_only_stored_summaries()
    {
        await using var rig = await TestRig.CreateAsync(activate: false);
        using var owner = rig.Client();
        using var other = rig.Client(owner: OtherOwner);
        var now = rig.Clock.UtcNow;
        var quotes = Enumerable.Range(1, 105).Select(i => Snapshot(i, now.AddSeconds(-i))).ToArray();
        var foreign = Snapshot(106, now.AddMinutes(1), owner: OtherOwner);
        var paid = Snapshot(107, now.AddMinutes(1));
        paid.Status = QuoteStatus.Paid;
        paid.PaymentId = Guid.NewGuid();
        await rig.InDb(async db => { db.Quotes.AddRange(quotes.Append(foreign).Append(paid)); await db.SaveChangesAsync(); });

        var first = await TestRig.Data(await owner.GetAsync("/api/quotes"));
        Assert.Equal(1, first.GetProperty("page").GetInt32());
        Assert.Equal(20, first.GetProperty("pageSize").GetInt32());
        Assert.Equal(quotes.Take(20).Select(q => q.Id), Ids(first));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("quotes")[0].GetProperty("paymentId").ValueKind);
        Assert.Equal("Unpaid", first.GetProperty("quotes")[0].GetProperty("status").GetString());
        var summary = first.GetProperty("quotes")[0];
        Assert.Equal(10200, summary.GetProperty("totalMinorUnits").GetInt64());
        Assert.Equal("00123", summary.GetProperty("referenceValues")[0].GetProperty("value").GetString());

        var second = await TestRig.Data(await owner.GetAsync("/api/quotes?page=2"));
        Assert.Equal(quotes.Skip(20).Take(20).Select(q => q.Id), Ids(second));
        Assert.Equal(quotes.Take(100).Select(q => q.Id), Ids(await TestRig.Data(await owner.GetAsync("/api/quotes?pageSize=100"))));
        Assert.Empty(Ids(await TestRig.Data(await owner.GetAsync("/api/quotes?page=1000000"))));
        Assert.Equal(new[] { foreign.Id }, Ids(await TestRig.Data(await other.GetAsync("/api/quotes"))));

        var text = first.GetRawText();
        foreach (var privateField in new[] { "companyPaymentEndpoint", "credential", "clientId", "internal-source", "items" })
            Assert.DoesNotContain(privateField, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, rig.Provider.Calls);
        Assert.Equal(0, rig.Execution.ConfirmCalls);
        Assert.Equal(0, rig.Execution.StatusCalls);
    }

    [Fact]
    public async Task Status_service_and_inclusive_UTC_date_filters_combine_with_stable_ID_ties()
    {
        await using var rig = await TestRig.CreateAsync(activate: false);
        using var client = rig.Client();
        var now = rig.Clock.UtcNow;
        var first = Snapshot(1, now);
        var second = Snapshot(2, now);
        var older = Snapshot(3, now.AddMinutes(-1));
        var differentService = Snapshot(4, now);
        differentService.ServiceId = Guid.NewGuid();
        var cancelled = Snapshot(5, now);
        cancelled.Status = QuoteStatus.Cancelled;
        await rig.InDb(async db =>
        {
            db.Quotes.AddRange(first, second, older, differentService, cancelled);
            await db.SaveChangesAsync();
        });

        // The +02:00 endpoints denote exactly the same UTC instant as creation.
        var date = Uri.EscapeDataString(new DateTimeOffset(now).ToOffset(TimeSpan.FromHours(2)).ToString("O"));
        var query = $"/api/quotes?status=Unpaid&serviceId={first.ServiceId}&createdFrom={date}&createdTo={date}&pageSize=1";
        Assert.Equal(new[] { second.Id }, Ids(await TestRig.Data(await client.GetAsync(query))));
        Assert.Equal(new[] { first.Id }, Ids(await TestRig.Data(await client.GetAsync(query + "&page=2"))));
        Assert.Empty(Ids(await TestRig.Data(await client.GetAsync(query + "&page=3"))));
        Assert.Empty(Ids(await TestRig.Data(await client.GetAsync("/api/quotes?status=Cancelled"))));
    }

    [Fact]
    public async Task Detail_list_and_replayed_creation_share_effective_expiry_without_writes_or_provider_calls()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var inquiry = await TestRig.Data(await rig.Inquire(client));
        var inquiryId = inquiry.GetProperty("id").GetGuid();
        var created = await TestRig.Data(await rig.Quote(client, inquiryId));
        var id = created.GetProperty("id").GetGuid();
        var deadline = created.GetProperty("expiresAt").GetDateTime();
        var processing = Snapshot(1000, rig.Clock.UtcNow);
        processing.Status = QuoteStatus.Processing;
        processing.ExpiresAt = deadline;
        processing.PaymentId = Guid.NewGuid();
        await rig.InDb(async db => { db.Quotes.Add(processing); await db.SaveChangesAsync(); });

        rig.Clock.UtcNow = deadline.AddTicks(-1);
        Assert.Equal("Unpaid", (await TestRig.Data(await client.GetAsync($"/api/quotes/{id}"))).GetProperty("status").GetString());
        rig.Clock.UtcNow = deadline;
        var detail = await TestRig.Data(await client.GetAsync($"/api/quotes/{id}"));
        Assert.Equal("Expired", detail.GetProperty("status").GetString());
        var history = await TestRig.Data(await client.GetAsync("/api/quotes?status=Expired"));
        Assert.Equal(new[] { id }, Ids(history));
        Assert.Empty(Ids(await TestRig.Data(await client.GetAsync("/api/quotes?status=Unpaid"))));
        Assert.Empty(Ids(await TestRig.Data(await client.GetAsync("/api/quotes?status=Processing"))));
        var replay = await TestRig.Data(await rig.Quote(client, inquiryId));
        Assert.Equal("Expired", replay.GetProperty("status").GetString());
        Assert.Equal(created.GetProperty("expiresAt").GetDateTime(), replay.GetProperty("expiresAt").GetDateTime());
        Assert.Equal(
            created.GetProperty("items").Deserialize<QuoteLineDto[]>(JsonSerializerOptions.Web),
            detail.GetProperty("items").Deserialize<QuoteLineDto[]>(JsonSerializerOptions.Web));
        await rig.InDb(async db => Assert.Equal(QuoteStatus.Unpaid, (await db.Quotes.SingleAsync(q => q.Id == id)).Status));
        Assert.Equal(1, rig.Provider.Calls);
        Assert.Equal(0, rig.Execution.ConfirmCalls);
        Assert.Equal(0, rig.Execution.StatusCalls);
    }

    [Fact]
    public async Task Invalid_bounds_filters_dates_and_repeated_query_values_are_bad_requests()
    {
        await using var rig = await TestRig.CreateAsync(activate: false);
        using var client = rig.Client();
        foreach (var query in new[]
        {
            "page=0", "page=-1", "page=1000001", "page=2147483647", "page=abc",
            "pageSize=0", "pageSize=-1", "pageSize=101", "status=99", "status=unknown",
            "serviceId=00000000-0000-0000-0000-000000000000", "serviceId=wrong", "status=",
            "createdFrom=wrong", "createdFrom=2026-11-05T00:00:00Z&createdTo=2026-11-04T00:00:00Z",
            "page=1&page=2", "status=Unpaid&status=Paid"
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/quotes?" + query)).StatusCode);
        }
        Assert.Equal(0, rig.Provider.Calls);
        Assert.Equal(0, rig.Execution.StatusCalls);
    }

    [Fact]
    public async Task Status_recovers_accepted_payment_after_local_commit_failure_and_expiry()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var inquiry = await TestRig.Data(await rig.Inquire(client));
        var quote = await TestRig.Data(await rig.Quote(client, inquiry.GetProperty("id").GetGuid()));
        var id = quote.GetProperty("id").GetGuid();
        rig.Faults.FailExecutionCommit = true;
        var confirm = await client.PostAsJsonAsync($"/api/quotes/{id}/confirm", new { creditCardId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, confirm.StatusCode);
        var accepted = rig.Execution.Results[(TestRig.Owner, id)];
        await rig.InDb(async db =>
        {
            var stored = await db.Quotes.SingleAsync(q => q.Id == id);
            Assert.Null(stored.PaymentId);
            Assert.Equal(QuoteStatus.Submitted, stored.Status);
        });

        rig.Clock.UtcNow = quote.GetProperty("expiresAt").GetDateTime().AddHours(1);
        var recovered = await TestRig.Data(await client.GetAsync($"/api/quotes/{id}/execution"));
        Assert.Equal(accepted.PaymentId, recovered.GetProperty("paymentId").GetGuid());
        Assert.Equal("Completed", recovered.GetProperty("status").GetString());
        var again = await TestRig.Data(await client.GetAsync($"/api/quotes/{id}/execution"));
        Assert.Equal(accepted.PaymentId, again.GetProperty("paymentId").GetGuid());
        Assert.Equal("Paid", (await TestRig.Data(await client.GetAsync($"/api/quotes/{id}"))).GetProperty("status").GetString());
        Assert.Single(rig.Execution.Results);
        Assert.Equal(1, rig.Execution.ConfirmCalls);
        Assert.Equal(3, rig.Execution.StatusCalls);
        Assert.Equal(2, rig.Provider.Calls);
        await rig.InDb(async db => Assert.Equal(accepted.PaymentId, (await db.Quotes.SingleAsync(q => q.Id == id)).PaymentId));
    }

    [Fact]
    public async Task Status_not_found_outage_and_foreign_quotes_do_not_mutate_or_submit()
    {
        await using var rig = await TestRig.CreateAsync(activate: false);
        using var owner = rig.Client();
        using var foreign = rig.Client(owner: OtherOwner);
        var quote = Snapshot(1, rig.Clock.UtcNow);
        quote.ExecutionErrorCode = "PRESERVED_ERROR";
        await rig.InDb(async db => { db.Quotes.Add(quote); await db.SaveChangesAsync(); });

        var hidden = await foreign.GetAsync($"/api/quotes/{quote.Id}/execution");
        var missing = await foreign.GetAsync($"/api/quotes/{Guid.NewGuid()}/execution");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await hidden.Content.ReadAsStringAsync());
        Assert.Equal(0, rig.Execution.StatusCalls);
        var notFound = await owner.GetAsync($"/api/quotes/{quote.Id}/execution");
        Assert.Equal(HttpStatusCode.BadRequest, notFound.StatusCode);
        var error = await notFound.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EXECUTION_NOT_FOUND", error.GetProperty("data").GetProperty("errorCode").GetString());
        rig.Execution.StatusUnavailable = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await owner.GetAsync($"/api/quotes/{quote.Id}/execution")).StatusCode);
        await rig.InDb(async db =>
        {
            var stored = await db.Quotes.SingleAsync();
            Assert.Equal(QuoteStatus.Unpaid, stored.Status);
            Assert.Equal("PRESERVED_ERROR", stored.ExecutionErrorCode);
            Assert.Null(stored.PaymentId);
        });
        Assert.Equal(0, rig.Execution.ConfirmCalls);
        Assert.Equal(0, rig.Provider.Calls);
    }

    private static Guid[] Ids(JsonElement response) => response.GetProperty("quotes").EnumerateArray()
        .Select(item => item.GetProperty("id").GetGuid()).ToArray();

    private static Quote Snapshot(int sequence, DateTime createdAt, string owner = TestRig.Owner) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{sequence:D12}"),
        ClientId = owner,
        ServiceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        CountryId = TestRig.CountryId,
        CountryCode = "EC",
        ServiceName = "Stored service name",
        Currency = "USD",
        CurrencyScale = 2,
        RoundingMode = "ToEven",
        TotalMinorUnits = 10200,
        TotalAmount = 102m,
        CreatedAt = createdAt,
        ExpiresAt = createdAt.AddMinutes(60),
        CompanyCredentialReference = "private-credential",
        CompanyPaymentEndpoint = "https://company.test/private",
        Items = [new QuoteItem
        {
            Id = Guid.NewGuid(), ComponentType = "ServiceAmount", Description = "Stored price",
            Source = "internal-source", Currency = "USD", Amount = 102m, MinorUnits = 10200
        }],
        ReferenceValues = [new QuoteReferenceValue { Id = Guid.NewGuid(), Key = "accountNumber", Value = "00123" }]
    };
}
