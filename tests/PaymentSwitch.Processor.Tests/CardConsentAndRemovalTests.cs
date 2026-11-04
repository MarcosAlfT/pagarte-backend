using System.Net;
using System.Text.Json;
using ExternalConnections.PaymentOperators.Config;
using ExternalConnections.PaymentOperators.PaymentOperators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Tests;

public sealed class CardConsentAndRemovalTests
{
	[Fact]
	public async Task Consent_is_required_current_and_audited_and_original_request_remains_replayable()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var policy = await CardTestRig.Data(await client.GetAsync("/api/creditcard/registrations/policy"));
		Assert.Equal("saved-card-v1", policy.GetProperty("consentVersion").GetString());
		Assert.Equal(HttpStatusCode.BadRequest, (await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "test" }, "no-consent")).StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, (await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "obsolete" }, "stale-consent")).StatusCode);
		var session = await rig.Start(client, "accepted");
		await rig.InDb(async db =>
		{
			var stored = await db.CardRegistrationSessions.SingleAsync();
			Assert.Equal("saved-card-v1", stored.ConsentVersion);
			Assert.Equal(rig.Clock.UtcNow, stored.ConsentAcceptedAt);
			Assert.Equal(CardTestRig.Owner, stored.ClientId);
		});
		rig.Settings["PaymentMethods:ConsentVersion"] = "saved-card-v2";
		await rig.RestartAsync();
		using var restarted = rig.Client();
		Assert.Equal(session.GetProperty("id").GetString(), (await rig.Start(restarted, "accepted")).GetProperty("id").GetString());
		Assert.Equal(HttpStatusCode.Conflict, (await CardTestRig.Send(restarted, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "saved-card-v1" }, "new-stale")).StatusCode);
	}

	[Fact]
	public async Task Provider_removal_is_durable_owner_only_and_recovers_a_lost_result_commit()
	{
		await using var rig = await CardTestRig.CreateAsync();
		Enable(rig);
		using var client = rig.Client();
		var id = await Register(rig, client);
		await client.DeleteAsync($"/api/creditcard/{id}");
		rig.Faults.FailRemovalCommit = true;
		await Assert.ThrowsAsync<DbUpdateException>(() => Detach(rig, id));
		await rig.InDb(async db => { var card = await db.CreditCards.SingleAsync(); Assert.Equal("Pending", card.DetachmentState); Assert.Equal(1, card.DetachmentAttempts); });
		Assert.True(await RemovedAtProvider(rig, id));
		await rig.RestartAsync();
		rig.Clock.Advance(TimeSpan.FromMinutes(3));
		await Detach(rig, id);
		using var restarted = rig.Client();
		var view = await CardTestRig.Data(await restarted.GetAsync($"/api/creditcard/{id}/removal"));
		Assert.Equal("Removed", view.GetProperty("providerRemovalState").GetString());
		Assert.DoesNotContain("mock-card-", view.GetRawText());
		using var other = rig.Client(CardTestRig.Foreign);
		Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/creditcard/{id}/removal")).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/creditcard/{Guid.NewGuid()}/removal")).StatusCode);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Accepted_work_and_refund_guarantee_control_detachment(bool refundsSurvive)
	{
		await using var rig = await CardTestRig.CreateAsync();
		Enable(rig, refundsSurvive);
		using var client = rig.Client();
		var id = await Register(rig, client);
		rig.Company.Pause = true;
		var accepted = rig.Accept(id);
		await rig.Company.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await client.DeleteAsync($"/api/creditcard/{id}");
		await Detach(rig, id);
		Assert.False(await RemovedAtProvider(rig, id));
		await rig.InDb(async db => Assert.Equal("Deferred", (await db.CreditCards.SingleAsync()).DetachmentState));
		rig.Company.Release.TrySetResult();
		Assert.True((await accepted).Accepted);
		rig.Clock.Advance(TimeSpan.FromMinutes(2));
		await Detach(rig, id);
		Assert.Equal(refundsSurvive, await RemovedAtProvider(rig, id));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task A_shared_active_reference_is_never_detached_by_an_older_deleted_card(bool differentIntegrationId)
	{
		await using var rig = await CardTestRig.CreateAsync();
		Enable(rig);
		using var client = rig.Client();
		var first = await Register(rig, client, "same-instrument");
		await client.DeleteAsync($"/api/creditcard/{first}");
		if (differentIntegrationId)
		{
			foreach (var setting in rig.Settings.GetSection("PaymentOperators:Integrations:mock-a").AsEnumerable().ToArray())
				rig.Settings[setting.Key.Replace("Integrations:mock-a", "Integrations:mock-c")] = setting.Value;
			using var routeScope = rig.Services.CreateScope();
			await routeScope.ServiceProvider.GetRequiredService<PinPaymentOperatorRoutesUseCase>().ExecuteAsync();
			rig.Settings["PaymentOperators:DefaultIntegration"] = "mock-c";
		}
		var second = await Register(rig, client, "same-instrument");
		await Detach(rig, first);
		Assert.False(await RemovedAtProvider(rig, second));
		await rig.InDb(async db => Assert.Equal("Deferred", (await db.CreditCards.SingleAsync(c => c.Id == first)).DetachmentState));
		Assert.True((await rig.Accept(second)).Accepted);
	}

	[Fact]
	public async Task Pending_registration_defers_removal_and_uncertain_removal_prevents_new_registration()
	{
		await using var rig = await CardTestRig.CreateAsync();
		Enable(rig);
		using var client = rig.Client();
		var id = await Register(rig, client);
		await rig.Start(client);
		await client.DeleteAsync($"/api/creditcard/{id}");
		await Detach(rig, id);
		Assert.False(await RemovedAtProvider(rig, id));
		await rig.InDb(async db => { var card = await db.CreditCards.SingleAsync(); card.DetachmentState = "Pending"; card.DetachmentAttempts = 1; await db.SaveChangesAsync(); });
		Assert.Equal(HttpStatusCode.Conflict, (await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "saved-card-v1" }, "blocked")).StatusCode);
	}

	[Fact]
	public async Task Expired_leases_at_attempt_cap_do_not_make_a_sixth_removal_or_cleanup_call()
	{
		await using var rig = await CardTestRig.CreateAsync();
		Enable(rig);
		using var client = rig.Client();
		var id = await Register(rig, client);
		await client.DeleteAsync($"/api/creditcard/{id}");
		await rig.InDb(async db => { var card = await db.CreditCards.SingleAsync(); card.DetachmentAttempts = 5; card.DetachmentLeaseUntil = rig.Clock.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); });
		await Detach(rig, id);
		Assert.False(await RemovedAtProvider(rig, id));
		// Simulate a terminal setup whose fifth claimed attempt lost its result commit.
		await rig.InDb(async db => { var setup = await db.CardRegistrationSessions.SingleAsync(); setup.State = CardRegistrationState.Cancelled; setup.CleanupState = "Pending"; setup.CleanupAttempts = 5; setup.NextAttemptAt = rig.Clock.UtcNow; setup.LeaseUntil = rig.Clock.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); });
		Guid setupId = default;
		await rig.InDb(async db => setupId = (await db.CardRegistrationSessions.SingleAsync()).Id);
		await rig.Recover(setupId);
		Assert.False(await RemovedAtProvider(rig, id));
		await rig.InDb(async db => { Assert.Equal("RequiresReview", (await db.CreditCards.SingleAsync()).DetachmentState); var setup = await db.CardRegistrationSessions.SingleAsync(); Assert.Equal("RequiresReview", setup.CleanupState); Assert.Equal(5, setup.CleanupAttempts); Assert.Null(setup.NextAttemptAt); });
	}

	[Fact]
	public async Task Cancelled_setup_can_remove_its_isolated_resource_without_activating_a_card()
	{
		await using var rig = await CardTestRig.CreateAsync();
		Enable(rig);
		using var client = rig.Client();
		var setup = await rig.Start(client);
		var id = Guid.Parse(setup.GetProperty("id").GetString()!);
		await rig.Complete(client, setup, "challenge-success");
		await client.PostAsync($"/api/creditcard/registrations/{id}/cancel", null);
		await rig.Recover(id);
		await rig.InDb(async db => { Assert.Empty(await db.CreditCards.ToArrayAsync()); Assert.Equal("Removed", (await db.CardRegistrationSessions.SingleAsync()).CleanupState); });
	}

	[Theory]
	[InlineData("Adapter", "DLocal")]
	[InlineData("Account", "another-account")]
	[InlineData("Environment", "another-environment")]
	[InlineData("Version", "2")]
	[InlineData("CredentialReference", "another-credential")]
	public async Task Persisted_integration_identity_cannot_be_repointed_in_processor_or_worker(string field, string changed)
	{
		await using var rig = await CardTestRig.CreateAsync();
		var original = rig.Settings[$"PaymentOperators:Integrations:mock-a:{field}"];
		rig.Settings[$"PaymentOperators:Integrations:mock-a:{field}"] = changed;
		using var scope = rig.Services.CreateScope();
		Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IPaymentOperatorAdapterFactory>().GetRequiredAdapter("mock-a"));
		await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<PinPaymentOperatorRoutesUseCase>().ExecuteAsync());
		var guard = new PaymentOperatorRouteGuard();
		var loader = new PaymentSwitch.Worker.Services.PaymentOperatorRouteLoader(rig.Settings,
			rig.Services.GetRequiredService<PaymentOperatorRegistry>(), guard);
		await Assert.ThrowsAsync<InvalidOperationException>(() => loader.LoadAsync());
		rig.Settings[$"PaymentOperators:Integrations:mock-a:{field}"] = original;
		await loader.LoadAsync();
		guard.Validate(new("mock-a", "Mock", "synthetic-mock-a", "Development", "1"));
	}

	private static void Enable(CardTestRig rig, bool refundsSurvive = true)
	{
		rig.Settings["PaymentOperators:Integrations:mock-a:CardRemovalEnabled"] = "true";
		rig.Settings["PaymentOperators:Integrations:mock-a:RefundsSurviveDetachment"] = refundsSurvive.ToString();
	}
	private static async Task<Guid> Register(CardTestRig rig, HttpClient client, string scenario = "success") =>
		Guid.Parse((await CardTestRig.Data(await rig.Complete(client, await rig.Start(client), scenario))).GetProperty("cardId").GetString()!);
	private static async Task Detach(CardTestRig rig, Guid id)
	{
		using var scope = rig.Services.CreateScope();
		await scope.ServiceProvider.GetRequiredService<DetachCreditCardUseCase>().ExecuteAsync(id);
	}
	private static async Task<bool> RemovedAtProvider(CardTestRig rig, Guid id)
	{
		CreditCard card = null!;
		await rig.InDb(async db => card = await db.CreditCards.SingleAsync(c => c.Id == id));
		return rig.Services.GetRequiredService<MockCardSetupJournal>().IsRemoved(new(card.OperatorProvider, card.AdapterProvider,
			card.ProviderAccount, card.ProviderEnvironment, card.IntegrationVersion), card.OperatorCardToken);
	}
}
