using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayableServices.Application.Models;
using PayableServices.Infrastructure.Clients;
using PaymentSwitch.Contracts;

namespace PaymentSwitch.Processor.Tests;

public sealed class QuoteSubmissionGrpcTests
{
    [Fact]
    public async Task Actual_handoff_accepts_a_timely_claim_after_expiry_and_replays_without_a_second_charge()
    {
        await using var rig = await CardTestRig.CreateAsync();
        var card = await SaveCard(rig);
        var package = Package(rig, card) with { SubmittedAt = rig.Clock.UtcNow.AddMinutes(4).AddSeconds(59) };
        rig.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(2));
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        var result = await handoff.ConfirmQuoteAsync(package);
        Assert.True(result.Accepted);
        Assert.True(result.SubmissionRecorded);
        Assert.Equal(QuoteExecutionStatus.Completed, result.Status);
        rig.Clock.Advance(TimeSpan.FromDays(1));
        var replay = await handoff.ConfirmQuoteAsync(package);
        Assert.Equal(result.PaymentId, replay.PaymentId);
        Assert.True(replay.Replayed);
        Assert.Equal(1, rig.Company.Calls);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            Assert.Equal(1, payment.ChargeAttempts);
            Assert.Equal(package.SubmissionId, (await db.QuoteSubmissionOutcomes.SingleAsync()).SubmissionId);
            Assert.Equal(payment.Id, (await db.QuoteSubmissionOutcomes.SingleAsync()).PaymentId);
        });
        var conflict = await handoff.ConfirmQuoteAsync(package with { CreditCardId = Guid.NewGuid() });
        Assert.False(conflict.Accepted);
        Assert.Equal("SOURCE_QUOTE_FINGERPRINT_CONFLICT", conflict.ErrorCode);
        Assert.Equal(result.PaymentId, (await handoff.GetStatusAsync(package.ClientId, package.SourceQuoteId)).PaymentId);
    }

    [Fact]
    public async Task Rejection_is_a_SQL_tombstone_and_cannot_become_accepted_when_card_eligibility_changes()
    {
        await using var rig = await CardTestRig.CreateAsync();
        var card = await SaveCard(rig);
        await rig.InDb(async db => { (await db.CreditCards.SingleAsync(x => x.Id == card)).SetupVerified = false; await db.SaveChangesAsync(); });
        var package = Package(rig, card);
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        var rejected = await handoff.ConfirmQuoteAsync(package);
        Assert.False(rejected.Accepted);
        Assert.True(rejected.SubmissionRecorded);
        Assert.Equal("CARD_UNAVAILABLE", rejected.ErrorCode);
        await rig.InDb(async db => { (await db.CreditCards.SingleAsync(x => x.Id == card)).SetupVerified = true; await db.SaveChangesAsync(); });
        var replay = await handoff.ConfirmQuoteAsync(package);
        Assert.False(replay.Accepted);
        Assert.True(replay.Replayed);
        Assert.True(replay.SubmissionRecorded);
        Assert.Equal(rejected.ErrorCode, replay.ErrorCode);
        Assert.Equal(0, rig.Company.Calls);
        await rig.InDb(async db =>
        {
            Assert.Empty(await db.Payments.ToArrayAsync());
            var outcome = await db.QuoteSubmissionOutcomes.SingleAsync();
            Assert.Null(outcome.PaymentId);
            Assert.Equal("CARD_UNAVAILABLE", outcome.ErrorCode);
            var previous = db.Database.GetMigrations().Reverse().Skip(1).First();
            var blocked = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Equal(51000, blocked.Number);
        });
    }

    [Theory]
    [InlineData("deadline")]
    [InlineData("future")]
    public async Task Invalid_submission_time_is_rejected_without_side_effects_and_stays_rejected(string scenario)
    {
        await using var rig = await CardTestRig.CreateAsync();
        var card = await SaveCard(rig);
        var package = Package(rig, card);
        package = package with { SubmittedAt = scenario == "deadline" ? package.ExpiresAt : package.CreatedAt.AddMinutes(1) };
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        var response = await handoff.ConfirmQuoteAsync(package);
        Assert.False(response.Accepted);
        Assert.True(response.SubmissionRecorded);
        Assert.Equal("INVALID_EXECUTION_PACKAGE", response.ErrorCode);
        rig.Clock.Advance(TimeSpan.FromMinutes(10));
        Assert.False((await handoff.ConfirmQuoteAsync(package)).Accepted);
        Assert.Equal(0, rig.Company.Calls);
        await rig.InDb(async db => Assert.Empty(await db.Payments.ToArrayAsync()));
    }

    [Fact]
    public async Task Concurrent_grpc_deliveries_create_one_payment_and_one_acceptance_record()
    {
        await using var rig = await CardTestRig.CreateAsync();
        var package = Package(rig, await SaveCard(rig)) with { SubmittedAt = rig.Clock.UtcNow };
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => handoff.ConfirmQuoteAsync(package)));
        Assert.All(results, result => Assert.True(result.Accepted));
        Assert.Single(results.Select(result => result.PaymentId).Distinct());
        Assert.Equal(1, rig.Company.Calls);
        await rig.InDb(async db =>
        {
            Assert.Equal(1, await db.QuoteSubmissionOutcomes.CountAsync());
            Assert.Equal(1, (await db.Payments.SingleAsync()).ChargeAttempts);
        });
    }

    [Fact]
    public async Task New_rpc_requires_workload_authentication_and_legacy_rpc_cannot_start_a_new_payment()
    {
        await using var rig = await CardTestRig.CreateAsync();
        using var channel = rig.Channel();
        var grpc = new PaymentExecutionService.PaymentExecutionServiceClient(channel);
        var denied = await Assert.ThrowsAsync<RpcException>(async () =>
            await grpc.SubmitClaimedQuoteAsync(new SubmitClaimedQuoteRequest()));
        Assert.Equal(StatusCode.Unauthenticated, denied.StatusCode);
        var legacy = await grpc.ConfirmQuoteAsync(new ConfirmQuoteRequest
        {
            ClientId = CardTestRig.Owner, SourceQuoteId = Guid.NewGuid().ToString(), CreditCardId = Guid.NewGuid().ToString(),
            Snapshot = new QuoteExecutionSnapshot
            {
                ServiceId = Guid.NewGuid().ToString(), CountryId = Guid.NewGuid().ToString(), RouteId = Guid.NewGuid().ToString(),
                CreatedAt = rig.Clock.UtcNow.ToString("O"), ExpiresAt = rig.Clock.UtcNow.AddMinutes(60).ToString("O")
            }
        }, new Metadata { { "x-workload-api-key", CardTestRig.WorkloadKey } });
        Assert.False(legacy.Accepted);
        Assert.Equal("SUBMISSION_REQUIRED", legacy.ErrorCode);
        Assert.Equal(0, rig.Company.Calls);
    }

    private static async Task<Guid> SaveCard(CardTestRig rig)
    {
        using var client = rig.Client();
        var session = await rig.Start(client);
        var card = await CardTestRig.Data(await rig.Complete(client, session));
        return Guid.Parse(card.GetProperty("cardId").GetString()!);
    }

    private static QuoteExecutionPackage Package(CardTestRig rig, Guid card)
    {
        var created = rig.Clock.UtcNow;
        return new(CardTestRig.Owner, Guid.NewGuid(), card, Guid.NewGuid(), "Synthetic bill", Guid.NewGuid(), "EC",
            Guid.NewGuid(), "TestRoute", null, null, "Test", "TEST-SERVICE", "https://company.test/pay",
            "https://company.test/rollback", "https://company.test/status/{reference}", "test-credential",
            created, created.AddMinutes(5), "ToEven", 1000, "USD",
            [new ExecutionAmountComponent("ServiceAmount", "Service", "Test", true, 1, 1000, "USD")],
            [new ExecutionReferenceValue("account", "synthetic")], Guid.NewGuid(), created);
    }
}
