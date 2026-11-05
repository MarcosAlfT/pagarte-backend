using System.Security.Cryptography;
using System.Net;
using System.Text.Json;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PayableServices.Application.Models;
using PayableServices.Infrastructure.Clients;
using PaymentSwitch.Contracts;
using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Worker.Services;

namespace PaymentSwitch.Processor.Tests;

public sealed class ChargeFirstGrpcTests
{
    [Fact]
    public async Task Lost_charge_commit_and_lost_acknowledgment_recover_one_charge_and_one_company_notification()
    {
        await using var rig = await CardTestRig.CreateAsync(chargeFirst: true);
        var package = await CreatePackageAsync(rig);
        using (var channel = rig.Channel())
        {
            var handoff = Client(rig, channel);
            rig.Faults.LosePaymentTransition = PaymentTransactionStatus.ChargedAwaitingCompany;
            await Assert.ThrowsAsync<RpcException>(() => handoff.ConfirmQuoteAsync(package));
            Assert.Equal(0, rig.NotificationPosts);
        }
        Assert.Equal(1, rig.JournalEntries);
        var paymentId = Guid.Empty;
        await rig.InDb(async db => paymentId = (await db.Payments.SingleAsync()).Id);
        rig.Clock.Advance(TimeSpan.FromMinutes(6));
        await rig.RestartAsync();
        rig.DropNotificationResponse = true;
        await rig.ContinuePayment(paymentId);
        Assert.Equal(1, rig.NotificationPosts);
        Assert.Equal(1, rig.JournalEntries);
        using (var channel = rig.Channel())
            Assert.Equal(QuoteExecutionStatus.Processing,
                (await Client(rig, channel).GetStatusAsync(package.ClientId, package.SourceQuoteId)).Status);

        rig.Clock.Advance(TimeSpan.FromMinutes(6));
        await rig.RestartMockCompanyAsync();
        await rig.RestartAsync();
        await rig.ContinuePayment(paymentId);
        using var recoveredChannel = rig.Channel();
        var recovered = await Client(rig, recoveredChannel).GetStatusAsync(package.ClientId, package.SourceQuoteId);
        Assert.Equal(QuoteExecutionStatus.Completed, recovered.Status);
        Assert.Equal(1, rig.NotificationPosts);
        Assert.Equal(1, rig.NotificationLookups);
        Assert.Equal(0, rig.Company.Calls);
        Assert.Equal(1, rig.JournalEntries);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            Assert.True(payment.ChargeFirst);
            Assert.False(payment.BillReservationReleased);
            Assert.Equal(13045, payment.TotalMinorUnits);
        });
    }

    [Fact]
    public async Task Late_external_payment_refunds_full_charge_once_even_when_refund_result_commit_is_lost()
    {
        await using var rig = await CardTestRig.CreateAsync(chargeFirst: true);
        var package = await CreatePackageAsync(rig);
        // The accepted quote already has fresh unpaid evidence. Another channel settles the bill now.
        rig.MockCompanyResponses.SetBill("ELEC-EC", "10000001", new("2026-10", 0));
        using var channel = rig.Channel();
        var handoff = Client(rig, channel);
        rig.Faults.LosePaymentTransition = PaymentTransactionStatus.Compensated;
        await Assert.ThrowsAsync<RpcException>(() => handoff.ConfirmQuoteAsync(package));
        var pending = await handoff.GetStatusAsync(package.ClientId, package.SourceQuoteId);
        Assert.Equal(QuoteExecutionStatus.RefundPending, pending.Status);
        Assert.False(pending.Final);
        Assert.Equal(2, rig.JournalEntries);
        var duplicate = package with { SourceQuoteId = Guid.NewGuid(), SubmissionId = Guid.NewGuid() };
        Assert.Equal("BILL_PAYMENT_BLOCKED", (await handoff.ConfirmQuoteAsync(duplicate)).ErrorCode);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            var worker = new PaymentStatusRepository(rig.Settings, rig.Clock);
            Assert.False(await worker.IsCurrentLegacyRefundAsync(new RefundRequestMessage
            {
                PaymentId = payment.Id, OperatorProvider = payment.OperatorProvider,
                OperatorPaymentId = payment.OperatorPaymentId!, MinorUnits = payment.TotalMinorUnits, Currency = payment.Currency
            }, CancellationToken.None));
            await worker.UpdateStatusAsync(PaymentTransactionStatus.Refunded, payment.Id);
            await db.Entry(payment).ReloadAsync();
            Assert.Equal(PaymentTransactionStatus.RefundingCompensation, payment.Status);
        });

        rig.Clock.Advance(TimeSpan.FromMinutes(6));
        await rig.RestartAsync();
        await rig.ContinuePayment(pending.PaymentId!.Value);
        using var restartedChannel = rig.Channel();
        var refunded = await Client(rig, restartedChannel).GetStatusAsync(package.ClientId, package.SourceQuoteId);
        Assert.Equal(QuoteExecutionStatus.Refunded, refunded.Status);
        Assert.True(refunded.Final);
        Assert.Equal(2, rig.JournalEntries);
        Assert.Equal(1, rig.NotificationPosts);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            Assert.True(payment.BillReservationReleased);
            Assert.Equal("mock_refund_" + payment.CompensationKey, payment.OperatorRefundId);
        });
    }

    [Fact]
    public async Task Unconfirmed_company_at_retry_cap_stays_processing_without_refunding()
    {
        await using var rig = await CardTestRig.CreateAsync(chargeFirst: true);
        var package = await CreatePackageAsync(rig);
        rig.BlockNotificationResponse = true;
        using var channel = rig.Channel();
        var handoff = Client(rig, channel);
        var accepted = await handoff.ConfirmQuoteAsync(package);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            rig.Clock.Advance(TimeSpan.FromMinutes(6));
            await rig.ContinuePayment(accepted.PaymentId!.Value);
        }
        var pending = await handoff.GetStatusAsync(package.ClientId, package.SourceQuoteId);
        Assert.Equal(QuoteExecutionStatus.Processing, pending.Status);
        Assert.False(pending.Final);
        Assert.Equal("COMPANY_REVIEW_REQUIRED", pending.ErrorCode);
        Assert.Equal(1, rig.JournalEntries);
        await rig.InDb(async db => Assert.False((await db.Payments.SingleAsync()).BillReservationReleased));
    }

    [Fact]
    public async Task A_payment_accepted_before_enablement_keeps_company_first_order_when_recovered()
    {
        await using var rig = await CardTestRig.CreateAsync(chargeFirst: true);
        rig.Settings["ChargeFirst:Enabled"] = "false";
        await rig.RestartAsync();
        var package = await CreatePackageAsync(rig);
        rig.Faults.BeforePaymentInsert = () => rig.Clock.Advance(TimeSpan.FromMinutes(2));
        using (var channel = rig.Channel())
            Assert.Equal("BILL_VERIFICATION_STALE", (await Client(rig, channel).ConfirmQuoteAsync(package)).ErrorCode);

        rig.Settings["ChargeFirst:Enabled"] = "true";
        await rig.RestartAsync();
        using var restartedChannel = rig.Channel();
        var fresh = package.BillVerification! with
        {
            CheckedAt = rig.Clock.UtcNow, ExpiresAt = rig.Clock.UtcNow.AddMinutes(1)
        };
        var accepted = await Client(rig, restartedChannel).ConfirmQuoteAsync(package with { BillVerification = fresh });
        await rig.ContinuePayment(accepted.PaymentId!.Value);
        Assert.Equal(1, rig.Company.Calls);
        Assert.Equal(0, rig.NotificationPosts);
        await rig.InDb(async db =>
        {
            var payment = await db.Payments.SingleAsync();
            Assert.False(payment.ChargeFirst);
            Assert.Equal(PaymentTransactionStatus.Completed, payment.Status);
        });
    }

    [Fact]
    public async Task Charge_first_waits_for_fresh_evidence_and_rejects_an_unbound_billing_period()
    {
        await using var rig = await CardTestRig.CreateAsync(chargeFirst: true);
        var package = await CreatePackageAsync(rig);
        using var channel = rig.Channel();
        var handoff = Client(rig, channel);
        var unbound = package with
        {
            SourceQuoteId = Guid.NewGuid(), SubmissionId = Guid.NewGuid(),
            BillVerification = package.BillVerification! with { BillingPeriod = "2026-11" }
        };
        var rejected = await handoff.ConfirmQuoteAsync(unbound);
        Assert.Equal("BILL_IDENTITY_REQUIRED", rejected.ErrorCode);
        Assert.Null(rejected.PaymentId);

        rig.Faults.BeforePaymentInsert = () => rig.Clock.Advance(TimeSpan.FromMinutes(2));
        var waiting = await handoff.ConfirmQuoteAsync(package);
        Assert.Equal("BILL_VERIFICATION_STALE", waiting.ErrorCode);
        Assert.Equal(0, rig.JournalEntries);
        Assert.Equal(0, rig.NotificationPosts);
        var refreshed = package.BillVerification! with
        {
            CheckedAt = rig.Clock.UtcNow, ExpiresAt = rig.Clock.UtcNow.AddMinutes(1)
        };
        await handoff.ConfirmQuoteAsync(package with { BillVerification = refreshed });
        await rig.ContinuePayment(waiting.PaymentId!.Value);
        Assert.Equal(QuoteExecutionStatus.Completed,
            (await handoff.GetStatusAsync(package.ClientId, package.SourceQuoteId)).Status);
    }

    [Theory]
    [InlineData("Charge")]
    [InlineData("CompanyAcknowledgment")]
    [InlineData("Refund")]
    public async Task An_admin_can_schedule_one_audited_recovery_of_the_original_operation(string stage)
    {
        await using var rig = await CardTestRig.CreateAsync(chargeFirst: true);
        rig.Settings["PaymentExecution:MaxChargeAttempts"] = "1";
        rig.Settings["PaymentExecution:MaxCompanyPaymentAttempts"] = "1";
        rig.Settings["PaymentExecution:MaxCompensationAttempts"] = "1";
        await rig.RestartAsync();
        var package = await CreatePackageAsync(rig);
        using var channel = rig.Channel();
        var handoff = Client(rig, channel);
        if (stage == "CompanyAcknowledgment")
        {
            rig.DropNotificationResponse = true;
            await handoff.ConfirmQuoteAsync(package);
        }
        else
        {
            if (stage == "Refund") rig.MockCompanyResponses.SetBill("ELEC-EC", "10000001", new("2026-10", 0));
            rig.Faults.LosePaymentTransition = stage == "Charge"
                ? PaymentTransactionStatus.ChargedAwaitingCompany : PaymentTransactionStatus.Compensated;
            await Assert.ThrowsAsync<RpcException>(() => handoff.ConfirmQuoteAsync(package));
            var interrupted = await handoff.GetStatusAsync(package.ClientId, package.SourceQuoteId);
            rig.Clock.Advance(TimeSpan.FromMinutes(6));
            await rig.ContinuePayment(interrupted.PaymentId!.Value);
        }

        var recovery = new PaymentRecoveryService.PaymentRecoveryServiceClient(channel);
        var denied = await Assert.ThrowsAsync<RpcException>(async () => await recovery.ListAsync(new() { Limit = 10 },
            new Metadata { { "x-workload-api-key", CardTestRig.WorkloadKey } }));
        Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
        var execution = new PaymentExecutionService.PaymentExecutionServiceClient(channel);
        denied = await Assert.ThrowsAsync<RpcException>(async () => await execution.GetExecutionStatusAsync(new(),
            new Metadata { { "x-workload-api-key", CardTestRig.RecoveryKey } }));
        Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
        using var customer = rig.Client();
        using var admin = rig.Client(role: "admin");
        const string path = "/api/admin/payments/recovery";
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(path)).StatusCode);
        var queue = await CardTestRig.Data(await admin.GetAsync(path));
        var item = Assert.Single(queue.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(stage, item.GetProperty("stage").GetString());
        var paymentId = Guid.Parse(item.GetProperty("paymentId").GetString()!);
        var input = new { expectedVersion = item.GetProperty("version").GetString(), reasonCode = "ProviderStatusAvailable" };
        var requestPath = path + "/" + paymentId;
        var first = await CardTestRig.Data(await CardTestRig.Send(admin, requestPath, input, "review-1"), HttpStatusCode.Accepted);
        var replay = await CardTestRig.Data(await CardTestRig.Send(admin, requestPath, input, "review-1"), HttpStatusCode.Accepted);
        Assert.Equal(first.GetProperty("id").GetString(), replay.GetProperty("id").GetString());
        Assert.Equal(first.GetProperty("scheduledAt").GetString(), replay.GetProperty("scheduledAt").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await CardTestRig.Send(admin, requestPath, input, "review-2")).StatusCode);
        var audit = await CardTestRig.Data(await admin.GetAsync(path + "/audit/" + first.GetProperty("id").GetString()));
        Assert.Equal(stage, audit.GetProperty("stage").GetString());
        Assert.Contains(CardTestRig.Owner, audit.GetProperty("actor").GetString()!);
        await rig.InDb(async db => Assert.Single(await db.PaymentRecoveryActions.ToArrayAsync()));

        await rig.RestartAsync();
        await rig.ContinuePayment(paymentId);
        using var recoveredChannel = rig.Channel();
        var result = await Client(rig, recoveredChannel).GetStatusAsync(package.ClientId, package.SourceQuoteId);
        Assert.Equal(stage == "Refund" ? QuoteExecutionStatus.Refunded : QuoteExecutionStatus.Completed, result.Status);
        Assert.Equal(stage == "Refund" ? 2 : 1, rig.JournalEntries);
        Assert.Equal(1, rig.NotificationPosts);
    }

    private static PaymentSwitchExecutionClient Client(CardTestRig rig, Grpc.Net.Client.GrpcChannel channel) =>
        new(new PaymentExecutionService.PaymentExecutionServiceClient(channel), rig.Settings);

    private static async Task<QuoteExecutionPackage> CreatePackageAsync(CardTestRig rig)
    {
        using var client = rig.Client();
        var setup = await rig.Start(client);
        var card = (await CardTestRig.Data(await rig.Complete(client, setup))).GetProperty("cardId").GetGuid();
        var now = rig.Clock.UtcNow;
        var key = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new[] { "mock-company", "ELEC-EC", "10000001", "2026-10" })));
        return new(CardTestRig.Owner, Guid.NewGuid(), card, Guid.NewGuid(), "Electricity", Guid.NewGuid(), "EC",
            Guid.NewGuid(), "TestRoute", null, null, "MockCompany", "ELEC-EC", "http://localhost/payments",
            "http://localhost/payments/rollback", "http://localhost/payments/{reference}", "MockCompany",
            now, now.AddMinutes(5), "ToEven", 13045, "USD",
            [new("ServiceAmount", "Bill", "AmountInquiry", true, 1, 12845, "USD"), new("Fee", "Fee", "Catalogue", true, 2, 200, "USD")],
            [new("accountNumber", "10000001")], Guid.NewGuid(), now, key,
            new(null, ProviderInquiryId: "fresh-bill-evidence", ObservedBillKey: key, AmountMinorUnits: 12845,
                Currency: "USD", CheckedAt: now, ExpiresAt: now.AddMinutes(1), BillingPeriod: "2026-10"));
    }
}
