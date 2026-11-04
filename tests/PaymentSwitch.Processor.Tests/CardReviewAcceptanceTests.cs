using System.Net;
using System.Text.Json;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Tests;

public sealed class CardReviewAcceptanceTests
{
	[Fact]
	public async Task Review_boundary_requires_verified_admin_and_separate_internal_workload_key()
	{
		await using var rig = await CardTestRig.CreateAsync();
		foreach (var role in new[] { "client", "support" })
		{
			using var forbidden = rig.Client(role: role);
			Assert.Equal(HttpStatusCode.Forbidden, (await forbidden.GetAsync("/api/admin/payment-methods/reviews")).StatusCode);
		}
		using var anonymous = rig.Client(null);
		Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/payment-methods/reviews")).StatusCode);
		using var unverified = rig.Client(role: "admin", verified: false);
		Assert.Equal(HttpStatusCode.Forbidden, (await unverified.GetAsync("/api/admin/payment-methods/reviews")).StatusCode);
		using var admin = rig.Client(role: "admin");
		Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/payment-methods/reviews")).StatusCode);
		using var channel = rig.Channel();
		var client = new CardOperationsService.CardOperationsServiceClient(channel);
		var error = await Assert.ThrowsAsync<RpcException>(async () => await client.ListReviewsAsync(new(), new Metadata { { "x-workload-api-key", CardTestRig.WorkloadKey } }));
		Assert.Equal(StatusCode.PermissionDenied, error.StatusCode);
		Assert.Empty((await client.ListReviewsAsync(new(), new Metadata { { "x-workload-api-key", CardTestRig.OperationsKey } })).Items);
	}

	[Fact]
	public async Task Registration_recheck_uses_provider_evidence_and_is_audited_idempotent_and_does_not_reset_automatic_budget()
	{
		await using var rig = await CardTestRig.CreateAsync();
		var id = await Unresolved(rig);
		using var admin = rig.Client(role: "admin");
		var queue = await CardTestRig.Data(await admin.GetAsync("/api/admin/payment-methods/reviews"));
		Assert.Single(queue.GetProperty("items").EnumerateArray());
		Assert.DoesNotContain("mock-card-", queue.GetRawText());
		var result = await CardTestRig.Data(await Review(admin, id, "Registration", key: "recheck"));
		Assert.Equal("Succeeded", result.GetProperty("afterState").GetString());
		var replay = await CardTestRig.Data(await Review(admin, id, "Registration", key: "recheck"));
		Assert.Equal(result.GetProperty("id").GetString(), replay.GetProperty("id").GetString());
		Assert.Equal(HttpStatusCode.Conflict, (await Review(admin, id, "Registration", key: "recheck", reason: "ProviderStatusAvailable")).StatusCode);
		await rig.InDb(async db =>
		{
			Assert.Single(await db.CreditCards.ToArrayAsync());
			Assert.Equal(5, (await db.CardRegistrationSessions.SingleAsync()).Attempts);
			var audit = await db.CardReviewOperations.SingleAsync();
			Assert.Contains(CardTestRig.Owner, audit.Actor);
			Assert.Equal("Unresolved", audit.BeforeState); Assert.Equal("Succeeded", audit.AfterState); Assert.NotNull(audit.CompletedAt);
		});
		Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/payment-methods/reviews/{result.GetProperty("id").GetString()}/audit")).StatusCode);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Expired_or_cancelled_setup_cannot_be_activated_by_review(bool expire)
	{
		await using var rig = await CardTestRig.CreateAsync();
		var id = await Unresolved(rig);
		if (expire) rig.Clock.Advance(TimeSpan.FromMinutes(16));
		else await rig.InDb(async db => { var s = await db.CardRegistrationSessions.SingleAsync(); s.State = CardRegistrationState.Cancelled; await db.SaveChangesAsync(); });
		using var admin = rig.Client(role: "admin");
		var response = await Review(admin, id, "Registration");
		Assert.Equal(expire ? HttpStatusCode.OK : HttpStatusCode.Conflict, response.StatusCode);
		await rig.InDb(async db => Assert.Empty(await db.CreditCards.ToArrayAsync()));
	}

	[Fact]
	public async Task Unknown_provider_result_retains_review_state_without_a_new_save_attempt()
	{
		await using var rig = await CardTestRig.CreateAsync();
		var id = await Unresolved(rig, "timeout");
		using var admin = rig.Client(role: "admin");
		var response = await CardTestRig.Data(await Review(admin, id, "Registration"));
		Assert.Equal("Unknown", response.GetProperty("outcome").GetString());
		Assert.Equal("Unresolved", response.GetProperty("afterState").GetString());
		await rig.InDb(async db => { Assert.Empty(await db.CreditCards.ToArrayAsync()); Assert.Equal(5, (await db.CardRegistrationSessions.SingleAsync()).Attempts); });
	}

	[Fact]
	public async Task Removal_review_can_verify_absence_or_retry_idempotent_deletion_with_an_audit()
	{
		await using var rig = await CardTestRig.CreateAsync();
		EnableRemoval(rig);
		var id = await RemovedCardForReview(rig);
		using var admin = rig.Client(role: "admin");
		Assert.Equal("RequiresReview", (await CardTestRig.Data(await Review(admin, id, "CardRemoval", key: "lookup"))).GetProperty("afterState").GetString());
		var removed = await CardTestRig.Data(await Review(admin, id, "CardRemoval", "RetryRemoval", "delete"));
		Assert.Equal("Removed", removed.GetProperty("afterState").GetString());
		await rig.InDb(async db => { var card = await db.CreditCards.SingleAsync(); Assert.True(card.IsDeleted); Assert.Equal(5, card.DetachmentAttempts); Assert.Equal(2, await db.CardReviewOperations.CountAsync()); });
	}

	[Fact]
	public async Task Admin_removal_retry_still_respects_accepted_payment_and_refund_dependencies()
	{
		await using var rig = await CardTestRig.CreateAsync();
		EnableRemoval(rig);
		using var customer = rig.Client();
		var saved = await CardTestRig.Data(await rig.Complete(customer, await rig.Start(customer)));
		var id = Guid.Parse(saved.GetProperty("cardId").GetString()!);
		Assert.True((await rig.Accept(id)).Accepted);
		await customer.DeleteAsync($"/api/creditcard/{id}");
		await MarkRemovalReview(rig);
		using var admin = rig.Client(role: "admin");
		Assert.Equal(HttpStatusCode.Conflict, (await Review(admin, id, "CardRemoval", "RetryRemoval")).StatusCode);
		await rig.InDb(async db => Assert.Empty(await db.CardReviewOperations.ToArrayAsync()));
	}

	[Fact]
	public async Task Lost_review_commit_retains_pending_audit_and_new_review_recovers_without_replaying_token()
	{
		await using var rig = await CardTestRig.CreateAsync();
		var id = await Unresolved(rig);
		using var admin = rig.Client(role: "admin");
		rig.Faults.FailReviewCommit = true;
		Assert.Equal(HttpStatusCode.InternalServerError, (await Review(admin, id, "Registration", key: "lost" )).StatusCode);
		var replay = await CardTestRig.Data(await Review(admin, id, "Registration", key: "lost"), HttpStatusCode.Accepted);
		Assert.Equal("Pending", replay.GetProperty("outcome").GetString());
		await rig.InDb(async db => Assert.Empty(await db.CreditCards.ToArrayAsync()));
		rig.Clock.Advance(TimeSpan.FromMinutes(3));
		var recovered = await CardTestRig.Data(await Review(admin, id, "Registration", key: "recover"));
		Assert.Equal("Succeeded", recovered.GetProperty("afterState").GetString());
		await rig.InDb(async db => { Assert.Single(await db.CreditCards.ToArrayAsync()); Assert.Equal("ResultUnknown", (await db.CardReviewOperations.SingleAsync(a => a.RequestKey == "lost")).Outcome); });
	}

	[Fact]
	public async Task Setup_cleanup_review_only_removes_its_isolated_provider_resource()
	{
		await using var rig = await CardTestRig.CreateAsync();
		EnableRemoval(rig);
		using var customer = rig.Client();
		var setup = await rig.Start(customer); var id = Guid.Parse(setup.GetProperty("id").GetString()!);
		await rig.Complete(customer, setup, "challenge-success");
		await customer.PostAsync($"/api/creditcard/registrations/{id}/cancel", null);
		await rig.InDb(async db => { var s = await db.CardRegistrationSessions.SingleAsync(); s.CleanupState = "RequiresReview"; s.CleanupAttempts = 5; s.NextAttemptAt = null; await db.SaveChangesAsync(); });
		using var admin = rig.Client(role: "admin");
		var result = await CardTestRig.Data(await Review(admin, id, "SetupCleanup", "RetryRemoval"));
		Assert.Equal("Removed", result.GetProperty("afterState").GetString());
		await rig.InDb(async db => Assert.Empty(await db.CreditCards.ToArrayAsync()));
	}

	[Fact]
	public async Task Stale_state_and_caller_supplied_actor_or_success_are_rejected()
	{
		await using var rig = await CardTestRig.CreateAsync();
		var id = await Unresolved(rig);
		using var admin = rig.Client(role: "admin");
		Assert.Equal(HttpStatusCode.Conflict, (await CardTestRig.Send(admin, $"/api/admin/payment-methods/reviews/Registration/{id}", new { action = "Reconcile", expectedState = "Succeeded", reasonCode = "CustomerSupportReview" }, "stale")).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await CardTestRig.Send(admin, $"/api/admin/payment-methods/reviews/Registration/{id}", new { action = "Reconcile", expectedState = "Unresolved", reasonCode = "CustomerSupportReview", actor = "spoofed", success = true }, "spoofed")).StatusCode);
		await rig.InDb(async db => Assert.Empty(await db.CardReviewOperations.ToArrayAsync()));
	}

	[Fact]
	public async Task Callback_transport_preserves_mock_auth_and_rejects_ambiguous_or_unbound_provider_headers()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client(null);
		using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment-method-callbacks/mock-a") {
			Content = new StringContent("{}") };
		request.Headers.Add("X-Operator-Signature", new[] { "one", "two" });
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
		using var unbound = new HttpRequestMessage(HttpMethod.Post, "/api/payment-method-callbacks/mock-a") { Content = new StringContent("{}") };
		unbound.Headers.TryAddWithoutValidation("Authorization", "V2-HMAC-SHA256, Signature: " + new string('0', 64));
		unbound.Headers.Add("X-Date", "2026-11-03T12:00:00.000Z");
		Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(unbound)).StatusCode);
		await rig.InDb(async db => Assert.Empty(await db.CardSetupNotifications.ToArrayAsync()));
	}

	private static Task<HttpResponseMessage> Review(HttpClient admin, Guid id, string kind, string action = "Reconcile", string key = "review", string reason = "CustomerSupportReview") =>
		CardTestRig.Send(admin, $"/api/admin/payment-methods/reviews/{kind}/{id}", new { action, expectedState = kind == "Registration" ? "Unresolved" : "RequiresReview", reasonCode = reason }, key);
	private static async Task<Guid> Unresolved(CardTestRig rig, string scenario = "save-before-timeout")
	{
		using var customer = rig.Client(); var setup = await rig.Start(customer); var id = Guid.Parse(setup.GetProperty("id").GetString()!);
		await rig.Complete(customer, setup, scenario);
		await rig.InDb(async db => { var s = await db.CardRegistrationSessions.SingleAsync(); s.State = CardRegistrationState.Unresolved; s.Attempts = 5; s.NextAttemptAt = null; await db.SaveChangesAsync(); });
		return id;
	}
	private static void EnableRemoval(CardTestRig rig) => rig.Settings["PaymentOperators:Integrations:mock-a:CardRemovalEnabled"] = "true";
	private static async Task<Guid> RemovedCardForReview(CardTestRig rig)
	{
		using var customer = rig.Client(); var setup = await CardTestRig.Data(await rig.Complete(customer, await rig.Start(customer)));
		var id = Guid.Parse(setup.GetProperty("cardId").GetString()!); await customer.DeleteAsync($"/api/creditcard/{id}"); await MarkRemovalReview(rig); return id;
	}
	private static async Task MarkRemovalReview(CardTestRig rig) => await rig.InDb(async db => { var card = await db.CreditCards.SingleAsync(); card.DetachmentState = "RequiresReview"; card.DetachmentAttempts = 5; card.DetachmentNextAttemptAt = null; await db.SaveChangesAsync(); });
}
