using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grpc.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentSwitch.Contracts;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Tests;

public sealed class CardLifecycleAcceptanceTests
{
	[Fact]
	public async Task Verified_setup_is_idempotent_and_exposes_only_safe_paginated_cards()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var session = await rig.Start(client, "create-one", isDefault: true);
		Assert.Equal(session.GetProperty("id").GetString(), (await rig.Start(client, "create-one", true)).GetProperty("id").GetString());
		var first = await CardTestRig.Data(await rig.Complete(client, session, key: "complete-one"));
		var replay = await CardTestRig.Data(await rig.Complete(client, session, key: "complete-one"));
		Assert.Equal("Succeeded", first.GetProperty("state").GetString());
		Assert.Equal(first.GetProperty("cardId").GetString(), replay.GetProperty("cardId").GetString());
		var conflict = await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "Different", consentAccepted = true, consentVersion = "saved-card-v1" }, "create-one");
		Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, (await rig.Complete(client, session, "decline", "complete-one")).StatusCode);
		var listResponse = await client.GetAsync("/api/creditcard?page=1&pageSize=1");
		var list = await CardTestRig.Data(listResponse);
		Assert.Single(list.EnumerateArray());
		Assert.True(list[0].GetProperty("available").GetBoolean());
		Assert.True(list[0].GetProperty("isDefault").GetBoolean());
		var text = await listResponse.Content.ReadAsStringAsync();
		Assert.DoesNotContain("mock-card-", text);
		Assert.DoesNotContain("temporaryToken", text, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("cardNumber", text, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("cvv", text, StringComparison.OrdinalIgnoreCase);
		Assert.Empty((await CardTestRig.Data(await client.GetAsync("/api/creditcard?page=2&pageSize=1"))).EnumerateArray());
		Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/creditcard?pageSize=101")).StatusCode);
		await rig.InDb(async db =>
		{
			Assert.Equal(1, await db.CreditCards.CountAsync());
			var saved = await db.CardRegistrationSessions.SingleAsync();
			Assert.Null(saved.ProtectedToken);
			Assert.NotNull(saved.TokenDigest);
			var columns = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID('CreditCards') AND name = 'CardNumber'").SingleAsync();
			Assert.Equal(0, columns);
		});
	}

	[Theory]
	[InlineData(null, true, HttpStatusCode.Unauthorized)]
	[InlineData(CardTestRig.Owner, false, HttpStatusCode.Forbidden)]
	public async Task Public_card_operations_require_verified_client(string? owner, bool verified, HttpStatusCode expected)
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client(owner, verified);
		Assert.Equal(expected, (await client.GetAsync("/api/creditcard")).StatusCode);
		Assert.Equal(expected, (await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "saved-card-v1" }, "key")).StatusCode);
	}

	[Fact]
	public async Task Missing_profile_and_missing_workload_credentials_fail_before_card_work()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		rig.MissingProfile = true;
		Assert.Equal(HttpStatusCode.Forbidden, (await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "saved-card-v1" }, "key")).StatusCode);
		using var channel = rig.Channel();
		var grpc = new CreditCardService.CreditCardServiceClient(channel);
		var error = await Assert.ThrowsAsync<RpcException>(async () => await grpc.GetCardsAsync(new GetCardsRequest { ClientId = CardTestRig.Owner }));
		Assert.Equal(StatusCode.Unauthenticated, error.StatusCode);
		var badKey = await Assert.ThrowsAsync<RpcException>(async () => await grpc.GetCardsAsync(new GetCardsRequest { ClientId = CardTestRig.Owner }, new Metadata { { "x-workload-api-key", "invalid" } }));
		Assert.Equal(StatusCode.Unauthenticated, badKey.StatusCode);
		await rig.InDb(async db => Assert.Empty(await db.CardRegistrationSessions.ToArrayAsync()));
	}

	[Fact]
	public async Task Legacy_fields_malformed_ids_and_foreign_resources_are_rejected_safely()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var owner = rig.Client();
		using var foreign = rig.Client(CardTestRig.Foreign);
		Assert.Equal(HttpStatusCode.BadRequest, (await CardTestRig.Send(owner, "/api/creditcard", new { cardNumber = "legacy-value", cvv = "legacy-value" })).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await CardTestRig.Send(owner, "/api/creditcard/registrations", new { cardHolderName = "test", cardNumber = "legacy-value" }, "raw")).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await CardTestRig.Send(owner, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "saved-card-v1" })).StatusCode);
		Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/creditcard/not-a-guid")).StatusCode);
		var session = await rig.Start(owner);
		var saved = await CardTestRig.Data(await rig.Complete(owner, session));
		var id = saved.GetProperty("cardId").GetString();
		var sessionId = session.GetProperty("id").GetString();
		Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/creditcard/{id}")).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/creditcard/{Guid.NewGuid()}")).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/creditcard/registrations/{sessionId}")).StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, (await foreign.GetAsync($"/api/creditcard/registrations/{Guid.NewGuid()}")).StatusCode);
		Assert.Empty((await CardTestRig.Data(await foreign.GetAsync("/api/creditcard"))).EnumerateArray());
		Assert.Equal(HttpStatusCode.OK, (await foreign.DeleteAsync($"/api/creditcard/{id}")).StatusCode);
		Assert.Single((await CardTestRig.Data(await owner.GetAsync("/api/creditcard"))).EnumerateArray());
	}

	[Theory]
	[InlineData("decline", "Failed", HttpStatusCode.UnprocessableEntity)]
	[InlineData("malformed", "Failed", HttpStatusCode.UnprocessableEntity)]
	[InlineData("challenge-success", "Succeeded", HttpStatusCode.OK)]
	[InlineData("challenge-decline", "Failed", HttpStatusCode.UnprocessableEntity)]
	[InlineData("challenge-cancel", "Cancelled", HttpStatusCode.OK)]
	[InlineData("save-before-timeout", "Succeeded", HttpStatusCode.OK)]
	[InlineData("unavailable", "Failed", HttpStatusCode.UnprocessableEntity)]
	public async Task Setup_outcomes_are_verified_by_provider_status(string scenario, string final, HttpStatusCode expected)
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var session = await rig.Start(client);
		var response = await rig.Complete(client, session, scenario);
		var id = session.GetProperty("id").GetString();
		if (scenario.StartsWith("challenge-")) Assert.Equal("RequiresAction", (await CardTestRig.Data(response)).GetProperty("state").GetString());
		rig.Clock.Advance(TimeSpan.FromSeconds(11));
		await rig.Recover(Guid.Parse(id!));
		var completed = await CardTestRig.Data(await client.GetAsync($"/api/creditcard/registrations/{id}"), expected);
		Assert.Equal(final, completed.GetProperty("state").GetString());
		await rig.Recover(Guid.Parse(id!));
		await rig.InDb(async db => Assert.Equal(final == "Succeeded" ? 1 : 0, await db.CreditCards.CountAsync()));
	}

	[Fact]
	public async Task Expiry_cancellation_and_retry_caps_prevent_later_activation()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		foreach (var cancel in new[] { false, true })
		{
			var session = await rig.Start(client);
			await rig.Complete(client, session, "challenge-expire");
			var id = session.GetProperty("id").GetString();
			if (cancel) await client.PostAsync($"/api/creditcard/registrations/{id}/cancel", null);
			else rig.Clock.Advance(TimeSpan.FromMinutes(16));
			await rig.Recover(Guid.Parse(id!));
			var status = await CardTestRig.Data(await client.GetAsync($"/api/creditcard/registrations/{id}"), cancel ? HttpStatusCode.OK : HttpStatusCode.Gone);
			Assert.Equal(cancel ? "Cancelled" : "Expired", status.GetProperty("state").GetString());
		}
		var timeout = await rig.Start(client);
		await rig.Complete(client, timeout, "timeout");
		for (var i = 0; i < 5; i++) { rig.Clock.Advance(TimeSpan.FromSeconds(11)); await rig.Recover(Guid.Parse(timeout.GetProperty("id").GetString()!)); }
		var unresolved = await CardTestRig.Data(await client.GetAsync($"/api/creditcard/registrations/{timeout.GetProperty("id").GetString()}"));
		Assert.Equal("Unresolved", unresolved.GetProperty("state").GetString());
		await rig.InDb(async db => Assert.Empty(await db.CreditCards.ToArrayAsync()));
	}

	[Fact]
	public async Task Successful_provider_save_recovers_after_database_failure_and_restart()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var session = await rig.Start(client);
		rig.Faults.FailCardCommit = true;
		Assert.Equal(HttpStatusCode.InternalServerError, (await rig.Complete(client, session, key: "complete")).StatusCode);
		await rig.InDb(async db =>
		{
			Assert.Empty(await db.CreditCards.ToArrayAsync());
			var state = await db.CardRegistrationSessions.SingleAsync();
			Assert.Equal(CardRegistrationState.Verifying, state.State);
			Assert.Null(state.ProtectedToken);
		});
		rig.Clock.Advance(TimeSpan.FromMinutes(3));
		await rig.RestartAsync();
		await rig.Recover(Guid.Parse(session.GetProperty("id").GetString()!));
		using var afterRestart = rig.Client();
		var status = await CardTestRig.Data(await afterRestart.GetAsync($"/api/creditcard/registrations/{session.GetProperty("id").GetString()}"));
		Assert.Equal("Succeeded", status.GetProperty("state").GetString());
		await rig.InDb(async db => Assert.Equal(1, await db.CreditCards.CountAsync()));
	}

	[Fact]
	public async Task Concurrent_completions_and_defaults_are_serialized_and_deletion_clears_default()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var firstSession = await rig.Start(client);
		var completions = await Task.WhenAll(rig.Complete(client, firstSession, key: "complete"), rig.Complete(client, firstSession, key: "complete"));
		Assert.All(completions, r => Assert.True(r.IsSuccessStatusCode));
		var secondSession = await rig.Start(client);
		var second = await CardTestRig.Data(await rig.Complete(client, secondSession));
		var first = await CardTestRig.Data(await client.GetAsync($"/api/creditcard/registrations/{firstSession.GetProperty("id").GetString()}"));
		var ids = new[] { first.GetProperty("cardId").GetString(), second.GetProperty("cardId").GetString() };
		var changes = await Task.WhenAll(ids.Select(id => client.PutAsJsonAsync($"/api/creditcard/{id}", new { cardHolderName = "Changed", isDefault = true })));
		Assert.All(changes, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
		string defaultId = "";
		await rig.InDb(async db =>
		{
			Assert.Equal(2, await db.CreditCards.CountAsync());
			defaultId = (await db.CreditCards.SingleAsync(c => c.IsDefault)).Id.ToString();
			var other = await db.CreditCards.SingleAsync(c => !c.IsDefault);
			other.IsDefault = true;
			var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
			Assert.IsType<SqlException>(error.InnerException);
		});
		Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/creditcard/{defaultId}")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/creditcard/{defaultId}")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/creditcard/{Guid.NewGuid()}")).StatusCode);
		var visible = await CardTestRig.Data(await client.GetAsync("/api/creditcard"));
		Assert.Single(visible.EnumerateArray());
		Assert.False(visible[0].GetProperty("isDefault").GetBoolean());
	}

	[Fact]
	public async Task Expired_cards_remain_visible_and_cannot_be_selected_as_default()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var saved = await CardTestRig.Data(await rig.Complete(client, await rig.Start(client)));
		var id = saved.GetProperty("cardId").GetString();
		rig.Clock.UtcNow = new DateTime(2041, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var card = await CardTestRig.Data(await client.GetAsync($"/api/creditcard/{id}"));
		Assert.False(card.GetProperty("available").GetBoolean());
		Assert.True(card.GetProperty("expired").GetBoolean());
		Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/creditcard/{id}", new { cardHolderName = "display only", isDefault = true })).StatusCode);
	}

	[Fact]
	public async Task Default_operator_switch_preserves_existing_sessions_cards_and_recovery()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var old = await rig.Start(client);
		rig.Settings["PaymentOperators:DefaultIntegration"] = "mock-b";
		rig.Settings["PaymentOperators:Integrations:mock-a:RegistrationEnabled"] = "false";
		var next = await rig.Start(client);
		Assert.Equal("mock-a", old.GetProperty("provider").GetString());
		Assert.Equal("mock-b", next.GetProperty("provider").GetString());
		Assert.Equal(HttpStatusCode.UnprocessableEntity, (await rig.Complete(client, next, token: CardTestRig.Token(old, "success"))).StatusCode);
		var saved = await CardTestRig.Data(await rig.Complete(client, old, "save-before-timeout"));
		Assert.Equal("Verifying", saved.GetProperty("state").GetString());
		rig.Clock.Advance(TimeSpan.FromSeconds(11));
		await rig.Recover(Guid.Parse(old.GetProperty("id").GetString()!));
		var card = (await CardTestRig.Data(await client.GetAsync("/api/creditcard")))[0];
		Assert.Equal("mock-a", card.GetProperty("operatorProvider").GetString());
		using var scope = rig.Services.CreateScope();
		var gateway = scope.ServiceProvider.GetRequiredService<ICardPaymentGateway>();
		var stored = await scope.ServiceProvider.GetRequiredService<PaymentSwitch.Processor.Interfaces.ICreditCardRepository>()
			.GetByIdAsync(Guid.Parse(card.GetProperty("id").GetString()!));
		Assert.Equal(PaymentOperationOutcome.Succeeded, (await gateway.CheckReadinessAsync(stored!)).Outcome);
	}

	[Fact]
	public async Task Completion_key_and_token_claims_cannot_be_reused_by_another_session()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var first = await rig.Start(client);
		var second = await rig.Start(client);
		await rig.Complete(client, first, key: "completion");
		Assert.Equal(HttpStatusCode.Conflict, (await rig.Complete(client, second, key: "completion")).StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, (await rig.Complete(client, second, token: CardTestRig.Token(first, "success"))).StatusCode);
	}

	[Fact]
	public async Task Removal_blocks_new_acceptance_but_preserves_accepted_execution_and_original_operator_refund()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var session = await rig.Start(client);
		var setup = await CardTestRig.Data(await rig.Complete(client, session));
		var cardId = Guid.Parse(setup.GetProperty("cardId").GetString()!);
		rig.Company.Pause = true;
		var acceptance = rig.Accept(cardId);
		await rig.Company.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
		// Acceptance has committed and released its owner lock before the company call.
		Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/creditcard/{cardId}")).StatusCode);
		rig.Settings["PaymentOperators:DefaultIntegration"] = "mock-b";
		rig.Settings["PaymentOperators:Integrations:mock-a:RegistrationEnabled"] = "false";
		rig.Company.Release.TrySetResult();
		var completed = await acceptance;
		Assert.True(completed.Accepted);
		Assert.Equal(PaymentExecutionResultStatus.Completed, completed.Status);
		Assert.False((await rig.Accept(cardId)).Accepted);
		Assert.Equal(1, rig.Company.Calls);
		await rig.InDb(async db =>
		{
			var card = await db.CreditCards.SingleAsync();
			Assert.True(card.IsDeleted);
			Assert.False(card.IsDefault);
			Assert.False(string.IsNullOrEmpty(card.OperatorCardToken));
			var payment = await db.Payments.SingleAsync();
			Assert.Equal("mock-a", payment.OperatorProvider);
			using var scope = rig.Services.CreateScope();
			var factory = scope.ServiceProvider.GetRequiredService<ExternalConnections.PaymentOperators.PaymentOperators.IPaymentOperatorAdapterFactory>();
			var refund = new PaymentSwitch.Worker.Services.PaymentOperatorRefundGateway(factory, new Utilities.Money.CurrencyCatalog());
			Assert.True((await refund.RefundAsync(payment.OperatorProvider, payment.OperatorPaymentId!, payment.TotalMinorUnits,
				payment.Currency, "synthetic refund after removal")).Success);
		});
	}

	[Fact]
	public async Task New_payment_acceptance_rejects_expired_unverified_and_unsupported_cards_before_company_payment()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var setup = await CardTestRig.Data(await rig.Complete(client, await rig.Start(client)));
		var id = Guid.Parse(setup.GetProperty("cardId").GetString()!);
		Assert.Equal("CARD_MARKET_UNSUPPORTED", (await rig.Accept(id, "BR")).ErrorCode);
		await rig.InDb(async db => { var card = await db.CreditCards.SingleAsync(); card.SetupVerified = false; await db.SaveChangesAsync(); });
		Assert.Equal("CARD_UNAVAILABLE", (await rig.Accept(id)).ErrorCode);
		await rig.InDb(async db => { var card = await db.CreditCards.SingleAsync(); card.SetupVerified = true; card.ExpiryYear = 2025; await db.SaveChangesAsync(); });
		Assert.Equal("CARD_UNAVAILABLE", (await rig.Accept(id)).ErrorCode);
		Assert.Equal(0, rig.Company.Calls);
		await rig.InDb(async db => Assert.Empty(await db.Payments.ToArrayAsync()));
	}

	[Theory]
	[InlineData("unknown", "1")]
	[InlineData("mock-a", "unsupported")]
	public async Task Unsupported_operator_or_version_fails_before_collecting_a_token(string selected, string version)
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		rig.Settings["PaymentOperators:DefaultIntegration"] = selected;
		rig.Settings["PaymentOperators:Integrations:mock-a:Version"] = version;
		Assert.Equal(HttpStatusCode.ServiceUnavailable, (await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "saved-card-v1" }, "key")).StatusCode);
		await rig.InDb(async db => Assert.Empty(await db.CardRegistrationSessions.ToArrayAsync()));
	}
	[Fact]
	public async Task Signed_callbacks_only_wake_provider_lookup_and_replays_and_late_events_cannot_activate_cards()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var session = await rig.Start(client);
		var id = Guid.Parse(session.GetProperty("id").GetString()!);
		await rig.Complete(client, session, "challenge-success");
		Assert.Equal(HttpStatusCode.Unauthorized, (await rig.Notify(id, "bad-signature", valid: false)).StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, (await rig.Notify(id, "too-old", timestamp: rig.Clock.UtcNow.AddMinutes(-6))).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await rig.Notify(id, "wrong-integration", "mock-b")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await rig.Notify(Guid.NewGuid(), "unknown-session")).StatusCode);
		await rig.InDb(async db => { Assert.Empty(await db.CardSetupNotifications.ToArrayAsync()); Assert.Empty(await db.CreditCards.ToArrayAsync()); });
		Assert.Equal(HttpStatusCode.OK, (await rig.Notify(id, "event-1")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await rig.Notify(id, "event-1")).StatusCode);
		await rig.InDb(async db => { Assert.Single(await db.CardSetupNotifications.ToArrayAsync()); Assert.Empty(await db.CreditCards.ToArrayAsync()); });
		await rig.Recover(id);
		Assert.Equal(HttpStatusCode.OK, (await rig.Notify(id, "late-event")).StatusCode);
		await rig.InDb(async db => Assert.Equal(1, await db.CreditCards.CountAsync()));
		var cancelled = await rig.Start(client);
		var cancelledId = Guid.Parse(cancelled.GetProperty("id").GetString()!);
		await rig.Complete(client, cancelled, "challenge-success");
		await client.PostAsync($"/api/creditcard/registrations/{cancelledId}/cancel", null);
		await rig.Notify(cancelledId, "late-cancelled");
		await rig.Recover(cancelledId);
		await rig.InDb(async db => Assert.Equal(1, await db.CreditCards.CountAsync()));
	}

	[Fact]
	public async Task Populated_legacy_database_keeps_card_rows_and_references_without_retaining_raw_card_storage()
	{
		await using var rig = await CardTestRig.CreateAsync(legacyCard: true);
		await rig.InDb(async db =>
		{
			var card = await db.CreditCards.SingleAsync();
			Assert.Equal("synthetic-legacy-reference", card.OperatorCardToken);
			Assert.Equal("Legacy display", card.CardHolderName);
			Assert.False(card.SetupVerified);
			Assert.False(card.IsDeleted);
			Assert.False(card.IsAvailable(rig.Clock.UtcNow));
			Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id = OBJECT_ID('CreditCards') AND name = 'CardNumber'").SingleAsync());
		});
	}

	[Fact]
	public async Task Migration_refuses_duplicate_legacy_defaults_instead_of_choosing_a_winner()
	{
		var error = await Assert.ThrowsAsync<SqlException>(async () => await CardTestRig.CreateAsync(duplicateLegacyDefaults: true));
		Assert.Contains("Resolve duplicate active card defaults", error.Message);
	}

	[Theory]
	[InlineData("mock-a", "success")]
	[InlineData("mock-b", "success")]
	[InlineData("mock-a", "challenge-success")]
	[InlineData("mock-b", "challenge-success")]
	public async Task Same_lifecycle_contract_runs_against_two_independent_configured_integrations(string integration, string scenario)
	{
		await using var rig = await CardTestRig.CreateAsync();
		rig.Settings["PaymentOperators:DefaultIntegration"] = integration;
		using var client = rig.Client();
		var session = await rig.Start(client);
		Assert.Equal(integration, session.GetProperty("provider").GetString());
		await rig.Complete(client, session, scenario, "same-key");
		rig.Clock.Advance(TimeSpan.FromSeconds(11));
		await rig.Recover(Guid.Parse(session.GetProperty("id").GetString()!));
		var replay = await CardTestRig.Data(await rig.Complete(client, session, scenario, "same-key"));
		Assert.Equal("Succeeded", replay.GetProperty("state").GetString());
		var id = replay.GetProperty("cardId").GetString();
		Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/creditcard/{id}")).StatusCode);
		Assert.Empty((await CardTestRig.Data(await client.GetAsync("/api/creditcard"))).EnumerateArray());
	}

	[Fact]
	public async Task Failed_default_commit_rolls_back_the_previous_default_clear()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var first = await CardTestRig.Data(await rig.Complete(client, await rig.Start(client, isDefault: true)));
		var second = await CardTestRig.Data(await rig.Complete(client, await rig.Start(client)));
		rig.Faults.FailDefaultCommit = true;
		var response = await client.PutAsJsonAsync($"/api/creditcard/{second.GetProperty("cardId").GetString()}", new { cardHolderName = "Changed", isDefault = true });
		Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
		await rig.InDb(async db => Assert.Equal(Guid.Parse(first.GetProperty("cardId").GetString()!), (await db.CreditCards.SingleAsync(c => c.IsDefault)).Id));
	}

	[Fact]
	public async Task Client_idempotency_keys_are_independent_and_tokens_cannot_cross_owners()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var owner = rig.Client();
		using var other = rig.Client(CardTestRig.Foreign);
		var first = await rig.Start(owner, "same-key");
		var second = await rig.Start(other, "same-key");
		Assert.NotEqual(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
		await rig.Complete(owner, first, key: "same-completion");
		Assert.Equal(HttpStatusCode.Conflict, (await rig.Complete(other, second, token: CardTestRig.Token(first, "success"))).StatusCode);
		Assert.Equal("Succeeded", (await CardTestRig.Data(await rig.Complete(other, second, key: "same-completion"))).GetProperty("state").GetString());
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Cancelled_or_expired_provider_resources_are_reconciled_without_activating_a_card(bool cancel)
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var session = await rig.Start(client);
		var id = Guid.Parse(session.GetProperty("id").GetString()!);
		await rig.Complete(client, session, "challenge-success");
		if (cancel) await client.PostAsync($"/api/creditcard/registrations/{id}/cancel", null);
		else { rig.Clock.Advance(TimeSpan.FromMinutes(16)); await rig.Recover(id); }
		await rig.Recover(id);
		var status = await CardTestRig.Data(await client.GetAsync($"/api/creditcard/registrations/{id}"), cancel ? HttpStatusCode.OK : HttpStatusCode.Gone);
		Assert.Equal(cancel ? "Cancelled" : "Expired", status.GetProperty("state").GetString());
		Assert.Equal("RetainedByProvider", status.GetProperty("cleanupState").GetString());
		await rig.InDb(async db => Assert.Empty(await db.CreditCards.ToArrayAsync()));
	}

	[Fact]
	public async Task Invalid_token_protection_configuration_fails_before_a_session_can_collect_data()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		rig.Settings["PaymentMethods:TokenDigestKey"] = "short";
		Assert.Equal(HttpStatusCode.ServiceUnavailable, (await CardTestRig.Send(client, "/api/creditcard/registrations", new { cardHolderName = "test", consentAccepted = true, consentVersion = "saved-card-v1" }, "key")).StatusCode);
		await rig.InDb(async db => Assert.Empty(await db.CardRegistrationSessions.ToArrayAsync()));
	}

	[Fact]
	public async Task Verified_provider_reference_deduplicates_new_tokens_and_allows_explicit_reregistration_after_removal()
	{
		await using var rig = await CardTestRig.CreateAsync();
		using var client = rig.Client();
		var first = await CardTestRig.Data(await rig.Complete(client, await rig.Start(client), "same-instrument"));
		var second = await CardTestRig.Data(await rig.Complete(client, await rig.Start(client), "same-instrument"));
		Assert.Equal(first.GetProperty("cardId").GetString(), second.GetProperty("cardId").GetString());
		await rig.InDb(async db => Assert.Equal(1, await db.CreditCards.CountAsync()));
		await client.DeleteAsync($"/api/creditcard/{first.GetProperty("cardId").GetString()}");
		var third = await CardTestRig.Data(await rig.Complete(client, await rig.Start(client), "same-instrument"));
		Assert.NotEqual(first.GetProperty("cardId").GetString(), third.GetProperty("cardId").GetString());
		await rig.InDb(async db => { Assert.Equal(2, await db.CreditCards.CountAsync()); Assert.Equal(1, await db.CreditCards.CountAsync(c => !c.IsDeleted)); });
	}

}
