using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PayableServices.Application.Models;
using PayableServices.Infrastructure.Clients;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.Abstractions;

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
        var recovered = await handoff.GetStatusAsync(package.ClientId, package.SourceQuoteId);
        Assert.True(recovered.Final);
        Assert.True(recovered.SubmissionRecorded);
        Assert.Equal(rejected.ErrorCode, recovered.ErrorCode);
        var hidden = await handoff.GetStatusAsync(CardTestRig.Foreign, package.SourceQuoteId);
        Assert.Equal("EXECUTION_NOT_FOUND", hidden.ErrorCode);
        Assert.False(hidden.SubmissionRecorded);
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
            var blocked = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.GetService<IMigrator>().MigrateAsync("20261104000501_AuditCardReviewsAndPinCredentialReferences"));
            Assert.Equal(51000, blocked.Number);
        });
    }

    [Fact]
    public async Task One_bill_reservation_blocks_different_quotes_and_clients_during_execution_and_after_success()
    {
        await using var rig = await CardTestRig.CreateAsync();
        const string other = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        var first = BillPackage(rig, await SaveCard(rig));
        var second = BillPackage(rig, await SaveCard(rig, other)) with { ClientId = other };
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        rig.Company.Pause = true;
        var paying = handoff.ConfirmQuoteAsync(first);
        try
        {
            await rig.Company.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var blocked = await handoff.ConfirmQuoteAsync(second);
            Assert.False(blocked.Accepted);
            Assert.True(blocked.SubmissionRecorded);
            Assert.Equal("BILL_PAYMENT_BLOCKED", blocked.ErrorCode);
            Assert.Null(blocked.PaymentId);
            Assert.Null(blocked.Reference);
        }
        finally { rig.Company.Release.TrySetResult(); }
        Assert.Equal(QuoteExecutionStatus.Completed, (await paying).Status);
        var later = second with { SourceQuoteId = Guid.NewGuid(), SubmissionId = Guid.NewGuid() };
        Assert.Equal("BILL_PAYMENT_BLOCKED", (await handoff.ConfirmQuoteAsync(later)).ErrorCode);
        Assert.Equal(1, rig.Company.Calls);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            Assert.Equal(first.BillKey, payment.BillKey);
            Assert.Equal(1, payment.ChargeAttempts);
            Assert.False(payment.BillReservationReleased);
            Assert.Equal(3, await db.QuoteSubmissionOutcomes.CountAsync());
        });

        var differentBill = BillPackage(rig, second.CreditCardId, new string('b', 64)) with { ClientId = other };
        Assert.Equal(QuoteExecutionStatus.Completed, (await handoff.ConfirmQuoteAsync(differentBill)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_work_with_stale_evidence_waits_for_fresh_verification_before_company_payment(bool billChanged)
    {
        await using var rig = await CardTestRig.CreateAsync();
        var package = BillPackage(rig, await SaveCard(rig));
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        rig.Faults.BeforePaymentInsert = () => rig.Clock.Advance(TimeSpan.FromMinutes(2));
        var accepted = await handoff.ConfirmQuoteAsync(package);
        Assert.True(accepted.Accepted);
        Assert.False(accepted.Final);
        Assert.Equal("BILL_VERIFICATION_STALE", accepted.ErrorCode);
        Assert.Equal(0, rig.Company.Calls);
        Assert.Equal(accepted.PaymentId, (await handoff.GetStatusAsync(package.ClientId, package.SourceQuoteId)).PaymentId);
        var secondQuote = BillPackage(rig, package.CreditCardId);
        rig.Clock.Advance(TimeSpan.FromSeconds(5));

        var refreshed = package.BillVerification! with
        {
            ErrorCode = billChanged ? "BILL_CHANGED" : null,
            ProviderInquiryId = "refreshed-inquiry",
            CheckedAt = rig.Clock.UtcNow,
            ExpiresAt = rig.Clock.UtcNow.AddMinutes(1)
        };
        var result = await handoff.ConfirmQuoteAsync(package with { BillVerification = refreshed });
        Assert.Equal(accepted.PaymentId, result.PaymentId);
        Assert.Equal(billChanged, result.Final);
        await rig.ContinuePayment(result.PaymentId!.Value);
        Assert.Equal(billChanged ? 0 : 1, rig.Company.Calls);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            Assert.Equal(billChanged ? 0 : 1, payment.ChargeAttempts);
            Assert.Equal(billChanged, payment.BillReservationReleased);
            Assert.Single(await db.QuoteSubmissionOutcomes.ToListAsync());
        });

        if (billChanged)
        {
            rig.Faults.BeforePaymentInsert = null;
            var stale = await handoff.ConfirmQuoteAsync(secondQuote);
            Assert.Equal("BILL_VERIFICATION_STALE", stale.ErrorCode);
            Assert.False(stale.SubmissionRecorded);
            var fresh = secondQuote.BillVerification! with
            {
                CheckedAt = rig.Clock.UtcNow, ExpiresAt = rig.Clock.UtcNow.AddMinutes(1)
            };
            Assert.Equal(QuoteExecutionStatus.Completed,
                (await handoff.ConfirmQuoteAsync(secondQuote with { BillVerification = fresh })).Status);
        }
    }

    [Fact]
    public async Task Stale_initial_evidence_can_be_refreshed_but_missing_identity_is_rejected_durably()
    {
        await using var rig = await CardTestRig.CreateAsync();
        var package = BillPackage(rig, await SaveCard(rig));
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        var legacy = package with { BillKey = null, BillVerification = null, SourceQuoteId = Guid.NewGuid(), SubmissionId = Guid.NewGuid() };
        var rejected = await handoff.ConfirmQuoteAsync(legacy);
        Assert.Equal("BILL_IDENTITY_REQUIRED", rejected.ErrorCode);
        Assert.True(rejected.SubmissionRecorded);
        rig.Clock.Advance(TimeSpan.FromMinutes(1));
        var stale = await handoff.ConfirmQuoteAsync(package);
        Assert.Equal("BILL_VERIFICATION_STALE", stale.ErrorCode);
        Assert.False(stale.SubmissionRecorded);
        Assert.False(stale.Final);
        Assert.Equal(0, rig.Company.Calls);
        var fresh = package.BillVerification! with { CheckedAt = rig.Clock.UtcNow, ExpiresAt = rig.Clock.UtcNow.AddMinutes(1) };
        Assert.Equal(QuoteExecutionStatus.Completed, (await handoff.ConfirmQuoteAsync(package with { BillVerification = fresh })).Status);
    }

    [Fact]
    public async Task Definitive_first_company_rejection_releases_the_bill_for_a_fresh_quote()
    {
        await using var rig = await CardTestRig.CreateAsync();
        var card = await SaveCard(rig);
        var package = BillPackage(rig, card);
        using var channel = rig.Channel();
        var handoff = new PaymentSwitchExecutionClient(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);
        rig.Company.Outcome = PaymentOperationOutcome.DefinitiveFailure;
        Assert.Equal(QuoteExecutionStatus.Declined, (await handoff.ConfirmQuoteAsync(package)).Status);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            Assert.True(payment.BillReservationReleased);
            Assert.Equal(0, payment.ChargeAttempts);
        });
        rig.Company.Outcome = PaymentOperationOutcome.Succeeded;
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(QuoteExecutionStatus.Completed, (await handoff.ConfirmQuoteAsync(BillPackage(rig, card))).Status);
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
        var billDenied = await Assert.ThrowsAsync<RpcException>(async () =>
            await grpc.SubmitVerifiedBillAsync(new SubmitVerifiedBillRequest()));
        Assert.Equal(StatusCode.Unauthenticated, billDenied.StatusCode);
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

    private static async Task<Guid> SaveCard(CardTestRig rig, string owner = CardTestRig.Owner)
    {
        using var client = rig.Client(owner);
        var session = await rig.Start(client);
        var card = await CardTestRig.Data(await rig.Complete(client, session));
        return Guid.Parse(card.GetProperty("cardId").GetString()!);
    }

    private static QuoteExecutionPackage BillPackage(CardTestRig rig, Guid card, string? key = null)
    {
        var package = Package(rig, card);
        key ??= new string('a', 64);
        return package with
        {
            BillKey = key,
            BillVerification = new(null, ProviderInquiryId: "verification-inquiry", ObservedBillKey: key,
                AmountMinorUnits: 1000, Currency: "USD", CheckedAt: rig.Clock.UtcNow, ExpiresAt: rig.Clock.UtcNow.AddMinutes(1)),
            TotalMinorUnits = 1025,
            Components = [package.Components.Single() with { Source = "AmountInquiry" },
                new("Fee", "Fee", "Catalogue", true, 2, 25, "USD")]
        };
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
