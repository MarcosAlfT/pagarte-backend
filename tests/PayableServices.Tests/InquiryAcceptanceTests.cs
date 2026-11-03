using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PayableServices.Domain.Entities;

namespace PayableServices.Tests;

public sealed class InquiryAcceptanceTests
{
    [Theory]
    [InlineData("10000001", "AmountDue", true)]
    [InlineData("10000002", "NothingDue", false)]
    [InlineData("10000003", "ReferenceNotFound", false)]
    [InlineData("10000004", "TemporarilyUnavailable", false)]
    [InlineData("10000005", "TemporarilyUnavailable", false)]
    [InlineData("10000006", "InvalidProviderResponse", false)]
    [InlineData("00000000", "ReferenceNotFound", false)]
    public async Task Http_scenarios_persist_replay_and_quote_only_positive_balances(string account, string outcome, bool payable)
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var inquiry = await TestRig.Data(await rig.Inquire(client, account, "scenario-key"));
        Assert.Equal(outcome, inquiry.GetProperty("outcome").GetString());
        var id = inquiry.GetProperty("id").GetGuid();
        var replay = await TestRig.Data(await rig.Inquire(client, " " + account + " ", "scenario-key"));
        Assert.Equal(id, replay.GetProperty("id").GetGuid());
        Assert.Equal(1, rig.Provider.Calls);
        var retrieved = await TestRig.Data(await client.GetAsync("/api/inquiries/" + id));
        Assert.Equal(outcome, retrieved.GetProperty("outcome").GetString());
        var quote = await rig.Quote(client, id);
        Assert.Equal(payable ? HttpStatusCode.OK : HttpStatusCode.BadRequest, quote.StatusCode);
        await rig.InDb(async db =>
        {
            var stored = await db.AmountInquiries.SingleAsync();
            Assert.Equal(account, stored.References()["accountNumber"]);
            Assert.NotEqual("scenario-key", stored.ProviderKey);
            Assert.Equal(TestRig.Owner, stored.ClientId);
            if (outcome is "AmountDue" or "NothingDue")
            {
                Assert.Equal(payable ? 12845L : 0L, stored.AmountMinorUnits);
                Assert.Equal("USD", stored.Currency);
                Assert.NotNull(stored.ProviderInquiryId);
            }
            else
            {
                Assert.Null(stored.AmountMinorUnits); Assert.Null(stored.Currency);
                Assert.Null(stored.ProviderInquiryId); Assert.Null(stored.ExpiresAt);
                Assert.NotNull(stored.ErrorCode);
            }
        });
    }

    [Fact]
    public async Task Idempotency_is_scoped_to_owner_and_conflicts_do_not_call_company()
    {
        await using var rig = await TestRig.CreateAsync();
        using var owner = rig.Client();
        using var foreign = rig.Client(owner: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var first = await TestRig.Data(await rig.Inquire(owner, key: "same-key"));
        var id = first.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await rig.Inquire(owner, "10000002", "same-key")).StatusCode);
        var second = await TestRig.Data(await rig.Inquire(foreign, key: "same-key"));
        Assert.NotEqual(id, second.GetProperty("id").GetGuid());
        Assert.Equal(2, rig.Provider.Keys.Distinct().Count());
        var missing = await foreign.GetAsync("/api/inquiries/" + Guid.NewGuid());
        var hidden = await foreign.GetAsync("/api/inquiries/" + id);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await hidden.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await rig.Quote(foreign, id)).StatusCode);
    }

    [Fact]
    public async Task Local_invalid_references_and_override_fields_never_reserve_or_call_provider()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        foreach (var references in new object[] {
            Array.Empty<object>(), new[] { new { key = "accountNumber", value = "bad" } },
            new[] { new { key = "AccountNumber", value = "10000001" } },
            new[] { new { key = "accountNumber", value = "10000001" }, new { key = "accountNumber", value = "10000001" } },
            new[] { new { key = "accountNumber", value = "1234567" } },
            new[] { new { key = "accountNumber", value = "123456789012345678901" } }
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/inquiries")
            {
                Content = JsonContent.Create(new { serviceId = rig.ServiceId, references })
            };
            request.Headers.Add("Idempotency-Key", "invalid-key");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
        }
        var over = await client.PostAsJsonAsync("/api/inquiries", new
        {
            serviceId = rig.ServiceId,
            references = new[] { new { key = "accountNumber", value = "10000001" } },
            amount = 1,
            currency = "EUR"
        });
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Equal(0, rig.Provider.Calls);
        await rig.InDb(async db => Assert.Empty(await db.AmountInquiries.ToListAsync()));
        await TestRig.Data(await rig.Inquire(client, key: "invalid-key"));
        Assert.Equal(1, rig.Provider.Calls);
    }

    [Fact]
    public async Task Concurrent_inquiries_return_pending_without_duplicate_provider_calls()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        rig.Provider.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Provider.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = rig.Inquire(client, key: "concurrent");
        await rig.Provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var duplicates = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => rig.Inquire(client, key: "concurrent")));
        Assert.All(duplicates, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        Assert.Equal(1, rig.Provider.Calls);
        rig.Provider.Release.SetResult();
        var completed = await TestRig.Data(await first);
        Assert.Equal("AmountDue", completed.GetProperty("outcome").GetString());
        var replay = await TestRig.Data(await rig.Inquire(client, key: "concurrent"));
        Assert.Equal(completed.GetProperty("id").GetGuid(), replay.GetProperty("id").GetGuid());
        await rig.InDb(async db => Assert.Equal(1, await db.AmountInquiries.CountAsync()));
    }

    [Fact]
    public async Task Persistence_failure_after_response_recovers_original_provider_key_and_evidence()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        rig.Faults.FailCompletion = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await rig.Inquire(client, key: "crash")).StatusCode);
        Guid id = default;
        await rig.InDb(async db =>
        {
            var pending = await db.AmountInquiries.SingleAsync(); id = pending.Id;
            Assert.Equal(InquiryOutcome.Pending, pending.Outcome); Assert.Null(pending.AmountMinorUnits);
        });
        var replay = await TestRig.Data(await rig.Inquire(client, key: "crash"));
        Assert.Equal("Pending", replay.GetProperty("outcome").GetString());
        Assert.Equal(1, rig.Provider.Calls);
        rig.Clock.UtcNow = rig.Clock.UtcNow.AddMinutes(2);
        await Task.WhenAll(rig.Recover(id), rig.Recover(id));
        var recovered = await TestRig.Data(await client.GetAsync("/api/inquiries/" + id));
        Assert.Equal("AmountDue", recovered.GetProperty("outcome").GetString());
        Assert.Equal(2, rig.Provider.Calls);
        Assert.Single(rig.Provider.Keys.Distinct());
        Assert.Equal(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc), recovered.GetProperty("providerResultAt").GetDateTime());
    }

    [Fact]
    public async Task Transport_timeout_is_persisted_and_replayed_without_new_call()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        rig.Provider.Timeout = true;
        var failed = await TestRig.Data(await rig.Inquire(client, key: "timeout"));
        Assert.Equal("TemporarilyUnavailable", failed.GetProperty("outcome").GetString());
        rig.Provider.Timeout = false;
        await TestRig.Data(await rig.Inquire(client, key: "timeout"));
        Assert.Equal(1, rig.Provider.Calls);
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("currency")]
    [InlineData("future")]
    [InlineData("expired")]
    [InlineData("reverse")]
    [InlineData("missingId")]
    [InlineData("references")]
    public async Task Invalid_provider_evidence_cannot_become_payable(string scenario)
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var now = rig.Clock.UtcNow;
        var body = new Dictionary<string, object?>
        {
            ["outcome"] = "amount_due",
            ["inquiry_id"] = scenario == "missingId" ? "" : "provider-id",
            ["service_code"] = "ELEC-EC",
            ["references"] = new { accountNumber = scenario == "references" ? "10000002" : "10000001" },
            ["amount_minor"] = scenario == "negative" ? -1L : 12845L,
            ["currency"] = scenario == "currency" ? "EUR" : "USD",
            ["result_at"] = scenario == "future" ? now.AddMinutes(1) : now,
            ["expires_at"] = scenario is "expired" or "reverse" ? now.AddMinutes(-1) : now.AddMinutes(15)
        };
        rig.Provider.Body = JsonSerializer.Serialize(body);
        if (scenario == "overflow") rig.Provider.Body = rig.Provider.Body.Replace("12845", "9223372036854775808");
        var result = await TestRig.Data(await rig.Inquire(client));
        Assert.Equal("InvalidProviderResponse", result.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("amountMinorUnits").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await rig.Quote(client, result.GetProperty("id").GetGuid())).StatusCode);
    }

    [Fact]
    public async Task Provider_short_expiry_limits_quote_and_replay_never_refreshes_lifetime()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var now = rig.Clock.UtcNow;
        rig.Provider.Body = JsonSerializer.Serialize(new
        {
            outcome = "amount_due",
            inquiry_id = "short",
            service_code = "ELEC-EC",
            references = new { accountNumber = "10000001" },
            amount_minor = 12845L,
            currency = "USD",
            result_at = now,
            expires_at = now.AddMinutes(3)
        });
        var inquiry = await TestRig.Data(await rig.Inquire(client, key: "short"));
        var id = inquiry.GetProperty("id").GetGuid();
        var quote = await TestRig.Data(await rig.Quote(client, id));
        Assert.Equal(now.AddMinutes(3), quote.GetProperty("expiresAt").GetDateTime());
        rig.Clock.UtcNow = now.AddMinutes(4);
        var replay = await TestRig.Data(await rig.Inquire(client, key: "short"));
        Assert.True(replay.GetProperty("expired").GetBoolean());
        Assert.Equal(now.AddMinutes(3), replay.GetProperty("expiresAt").GetDateTime());
        Assert.Equal(1, rig.Provider.Calls);
        // An unconsumed expired inquiry also rejects quote creation.
        await rig.InDb(async db =>
        {
            var stored = await db.AmountInquiries.SingleAsync(); stored.QuoteId = null;
            db.Quotes.RemoveRange(db.Quotes); await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await rig.Quote(client, id)).StatusCode);
    }

    [Fact]
    public async Task Quote_consumption_is_atomic_concurrent_and_uses_provider_base_once()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        await rig.InDb(async db =>
        {
            var composition = new AmountComposition { Id = Guid.NewGuid(), PayableServiceId = rig.ServiceId };
            foreach (var component in new[] { ("ServiceAmount", 999m), ("Fee", 2m), ("Tax", 0.1m), ("Discount", -0.05m) })
                composition.Components.Add(new AmountComponent
                {
                    Id = Guid.NewGuid(),
                    AmountCompositionId = composition.Id,
                    ComponentType = component.Item1,
                    Amount = component.Item2,
                    Currency = "USD"
                });
            db.AmountCompositions.Add(composition); await db.SaveChangesAsync();
        });
        var inquiry = await TestRig.Data(await rig.Inquire(client));
        var id = inquiry.GetProperty("id").GetGuid();
        rig.Faults.FailQuote = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await rig.Quote(client, id)).StatusCode);
        await rig.InDb(async db => { Assert.Empty(await db.Quotes.ToListAsync()); Assert.Null((await db.AmountInquiries.SingleAsync()).QuoteId); });
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => rig.Quote(client, id)));
        var quotes = await Task.WhenAll(responses.Select(TestRig.Data));
        Assert.Single(quotes.Select(x => x.GetProperty("id").GetGuid()).Distinct());
        Assert.All(quotes, quote => Assert.Equal(13050L, quote.GetProperty("totalMinorUnits").GetInt64()));
        var value = quotes[0].GetProperty("items").EnumerateArray().Single(x => x.GetProperty("componentType").GetString() == "ServiceAmount");
        Assert.Equal(12845L, value.GetProperty("minorUnits").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/quotes", new { inquiryId = id, currency = "USD" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/quotes", new { serviceId = rig.ServiceId, currency = "USD" })).StatusCode);
        await rig.InDb(async db => { Assert.Equal(1, await db.Quotes.CountAsync()); Assert.NotNull((await db.AmountInquiries.SingleAsync()).QuoteId); });
    }
    [Fact]
    public async Task Quote_snapshot_is_accepted_by_the_existing_execution_contract_and_confirmation_flow()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var inquiry = await TestRig.Data(await rig.Inquire(client));
        var quote = await TestRig.Data(await rig.Quote(client, inquiry.GetProperty("id").GetGuid()));
        var quoteId = quote.GetProperty("id").GetGuid();
        var response = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/confirm", new { creditCardId = Guid.NewGuid() });
        await TestRig.Data(response);
        Assert.Equal(12845L, rig.Execution.Package!.TotalMinorUnits);
        Assert.Equal("10000001", rig.Execution.Package.ReferenceValues.Single().Value);
        await rig.InDb(async db => Assert.Equal(PayableServices.Domain.Enums.QuoteStatus.Paid,
            (await db.Quotes.SingleAsync()).Status));
    }

    [Fact]
    public async Task Inquiry_expiry_is_capped_and_route_or_currency_changes_prevent_quoting()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        var now = rig.Clock.UtcNow;
        rig.Provider.Body = JsonSerializer.Serialize(new
        {
            outcome = "amount_due",
            inquiry_id = "long",
            service_code = "ELEC-EC",
            references = new { accountNumber = "10000001" },
            amount_minor = 12845L,
            currency = "USD",
            result_at = now,
            expires_at = now.AddHours(1)
        });
        var inquiry = await TestRig.Data(await rig.Inquire(client));
        var id = inquiry.GetProperty("id").GetGuid();
        Assert.Equal(now.AddMinutes(15), inquiry.GetProperty("expiresAt").GetDateTime());
        await rig.InDb(async db => { (await db.PayableServices.SingleAsync(x => x.Id == rig.ServiceId)).Currency = "EUR"; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.BadRequest, (await rig.Quote(client, id)).StatusCode);
        await rig.InDb(async db =>
        {
            (await db.PayableServices.SingleAsync(x => x.Id == rig.ServiceId)).Currency = "USD";
            (await db.PaymentRoutes.SingleAsync(x => x.Id == rig.RouteId)).ExternalServiceCode = "CHANGED"; await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await rig.Quote(client, id)).StatusCode);
        await rig.InDb(async db => { Assert.Empty(await db.Quotes.ToListAsync()); Assert.Null((await db.AmountInquiries.SingleAsync()).QuoteId); });
    }
    [Fact]
    public async Task Fingerprint_sorts_trimmed_fields_and_preserves_validated_optional_values()
    {
        await using var rig = await TestRig.CreateAsync();
        using var client = rig.Client();
        await rig.InDb(async db => { db.ReferenceFieldDefinitions.Add(new ReferenceFieldDefinition {
            Id = Guid.NewGuid(), PayableServiceId = rig.ServiceId, FieldKey = "memo", DisplayLabel = "Memo",
            DataType = "string", MaxLength = 20, IsRequired = false, DisplayOrder = 2 }); await db.SaveChangesAsync(); });
        async Task<JsonElement> Submit(object[] references)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/inquiries") {
                Content = JsonContent.Create(new { serviceId = rig.ServiceId, references }) };
            request.Headers.Add("Idempotency-Key", "ordered-fields");
            return await TestRig.Data(await client.SendAsync(request));
        }
        var first = await Submit([new { key = "memo", value = " " }, new { key = "accountNumber", value = " 10000001 " }]);
        var replay = await Submit([new { key = "accountNumber", value = "10000001" }, new { key = "memo", value = "" }]);
        Assert.Equal(first.GetProperty("id").GetGuid(), replay.GetProperty("id").GetGuid());
        Assert.Equal(1, rig.Provider.Calls);
        var quote = await TestRig.Data(await rig.Quote(client, first.GetProperty("id").GetGuid()));
        Assert.Contains(quote.GetProperty("referenceValues").EnumerateArray(), x => x.GetProperty("key").GetString() == "memo" && x.GetProperty("value").GetString() == "");
    }
}
