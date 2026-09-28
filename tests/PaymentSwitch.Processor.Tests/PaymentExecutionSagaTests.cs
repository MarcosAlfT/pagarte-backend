using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.UseCases;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Interfaces;
using Microsoft.Extensions.Options;
using Utilities.Money;

namespace PaymentSwitch.Processor.Tests;

public sealed class PaymentExecutionSagaTests
{
	[Fact]
	public async Task Confirmation_rejects_a_stored_card_owned_by_another_client()
	{
		var clock = new MutableClock(new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));
		var placeholder = CreatePayment(clock.UtcNow);
		var payments = new FakePaymentRepository(placeholder);
		var calls = new List<string>();
		var cards = new FakeCardGateway(calls);
		var company = new FakeCompanyGateway(calls);
		var continuation = CreateUseCase(placeholder, cards, company, clock);
		var foreignCard = CreditCard.Create(
			"different-client",
			"mock",
			"card-token",
			"************1111",
			"Other Client",
			"1111",
			CardType.Visa,
			12,
			2030,
			false,
			clock.UtcNow);
		var catalog = new CurrencyCatalog();
		var useCase = new AcceptConfirmedQuoteUseCase(
			payments,
			new FakeCreditCardRepository(foreignCard),
			cards,
			new ConfirmedQuoteValidator(catalog),
			continuation,
			catalog,
			Options.Create(new PaymentExecutionOptions()),
			clock);
		var execution = CreateExecution(foreignCard.Id, clock.UtcNow);

		var result = await useCase.ExecuteAsync(execution);

		Assert.False(result.Accepted);
		Assert.Equal("CARD_NOT_OWNED", result.ErrorCode);
		Assert.Empty(calls);
	}

	[Fact]
	public async Task Success_checks_card_then_pays_company_then_charges_full_total()
	{
		var clock = new MutableClock(new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));
		var payment = CreatePayment(clock.UtcNow);
		var calls = new List<string>();
		var cards = new FakeCardGateway(calls,
			Result(PaymentOperationOutcome.Succeeded),
			Result(PaymentOperationOutcome.Succeeded, "operator-payment"));
		var company = new FakeCompanyGateway(calls,
			Result(PaymentOperationOutcome.Succeeded, "company-payment"));
		var useCase = CreateUseCase(payment, cards, company, clock);

		await useCase.ExecuteAsync(payment.Id);

		Assert.Equal(
			["card-check", "company-pay", "card-charge"],
			calls);
		Assert.Equal(PaymentTransactionStatus.Completed, payment.Status);
		Assert.Equal(12_345, cards.ChargedMinorUnits);
		Assert.Equal("company-payment", payment.CompanyReference);
		Assert.Equal("operator-payment", payment.OperatorPaymentId);
		Assert.Equal(12_345, company.PaidMinorUnits);
	}

	[Fact]
	public async Task Retryable_charge_is_retried_without_repaying_company()
	{
		var clock = new MutableClock(new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));
		var payment = CreatePayment(clock.UtcNow);
		var calls = new List<string>();
		var cards = new FakeCardGateway(calls,
			Result(PaymentOperationOutcome.Succeeded),
			Result(PaymentOperationOutcome.RetryableFailure),
			Result(PaymentOperationOutcome.Succeeded, "operator-payment"));
		var company = new FakeCompanyGateway(calls,
			Result(PaymentOperationOutcome.Succeeded, "company-payment"));
		var useCase = CreateUseCase(payment, cards, company, clock);

		await useCase.ExecuteAsync(payment.Id);
		Assert.Equal(PaymentTransactionStatus.ChargeRetryScheduled, payment.Status);

		clock.Advance(TimeSpan.FromSeconds(payment.RetryIntervalSeconds));
		await useCase.ExecuteAsync(payment.Id);

		Assert.Equal(PaymentTransactionStatus.Completed, payment.Status);
		Assert.Equal(1, calls.Count(call => call == "company-pay"));
		Assert.Equal(2, calls.Count(call => call == "card-charge"));
		Assert.Equal(2, payment.ChargeAttempts);
	}

	[Fact]
	public async Task Definitive_charge_failure_rolls_back_company_payment()
	{
		var clock = new MutableClock(new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));
		var payment = CreatePayment(clock.UtcNow, maxChargeAttempts: 1);
		var calls = new List<string>();
		var cards = new FakeCardGateway(calls,
			Result(PaymentOperationOutcome.Succeeded),
			Result(PaymentOperationOutcome.DefinitiveFailure, code: "DECLINED"));
		var company = new FakeCompanyGateway(calls,
			Result(PaymentOperationOutcome.Succeeded, "company-payment"),
			Result(PaymentOperationOutcome.Succeeded));
		var useCase = CreateUseCase(payment, cards, company, clock);

		await useCase.ExecuteAsync(payment.Id);

		Assert.Equal(
			["card-check", "company-pay", "card-charge", "company-rollback"],
			calls);
		Assert.Equal(PaymentTransactionStatus.CompanyRolledBack, payment.Status);
		Assert.Equal("DECLINED", payment.ErrorCode);
		Assert.Equal(12_345, company.RolledBackMinorUnits);
	}

	[Fact]
	public async Task Unknown_charge_at_retry_cap_requires_manual_intervention_without_rollback()
	{
		var clock = new MutableClock(new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc));
		var payment = CreatePayment(clock.UtcNow, maxChargeAttempts: 1);
		var calls = new List<string>();
		var cards = new FakeCardGateway(calls,
			Result(PaymentOperationOutcome.Succeeded),
			Result(PaymentOperationOutcome.Unknown));
		var company = new FakeCompanyGateway(calls,
			Result(PaymentOperationOutcome.Succeeded, "company-payment"));
		var useCase = CreateUseCase(payment, cards, company, clock);

		await useCase.ExecuteAsync(payment.Id);

		Assert.Equal(PaymentTransactionStatus.ManualIntervention, payment.Status);
		Assert.DoesNotContain("company-rollback", calls);
	}

	private static ContinuePaymentExecutionUseCase CreateUseCase(
		Payment payment,
		FakeCardGateway cards,
		FakeCompanyGateway company,
		MutableClock clock) =>
		new(new FakePaymentRepository(payment), cards, company, new FakeUnitOfWork(), clock);

	private static ConfirmedQuoteExecution CreateExecution(Guid cardId, DateTime utcNow) =>
		new(
			"client-1",
			Guid.NewGuid(),
			cardId,
			Guid.NewGuid(),
			"Electricity",
			Guid.NewGuid(),
			"EC",
			Guid.NewGuid(),
			"QEC",
			null,
			null,
			"QEC",
			"ELECTRICITY",
			"https://company.test/pay",
			"https://company.test/rollback",
			"https://company.test/status/{reference}",
			"QEC",
			utcNow,
			utcNow.AddMinutes(15),
			"ToEven",
			12_345,
			"USD",
			[new ConfirmedQuoteComponent("ServiceAmount", "Electricity", "Catalogue", true, 1, 12_345, "USD")],
			[new ConfirmedQuoteReference("account", "123")]);

	private static Payment CreatePayment(DateTime utcNow, int maxChargeAttempts = 3)
	{
		var card = CreditCard.Create(
			"client-1",
			"mock",
			"card-token",
			"************1111",
			"Test Client",
			"1111",
			CardType.Visa,
			12,
			2030,
			true,
			utcNow);
		var payment = Payment.Create(
			new PaymentCreationData(
				"client-1",
				Guid.NewGuid(),
				new string('a', 64),
				card.Id,
				Guid.NewGuid(),
				"Electricity",
				Guid.NewGuid(),
				"EC",
				Guid.NewGuid(),
				"QEC",
				null,
				null,
				"QEC",
				"ELECTRICITY",
				"https://company.test/pay",
				"https://company.test/rollback",
				"https://company.test/status/{reference}",
				"QEC",
				12_345,
				"USD",
				2,
				"ToEven",
				utcNow,
				utcNow.AddMinutes(15),
				"mock",
				3,
				3,
				maxChargeAttempts,
				3,
				5),
			utcNow);
		payment.CreditCard = card;
		return payment;
	}

	private static PaymentOperationResult Result(
		PaymentOperationOutcome outcome,
		string? reference = null,
		string? code = null) =>
		new(outcome, reference, code, code);

	private sealed class MutableClock(DateTime utcNow) : IClock
	{
		public DateTime UtcNow { get; private set; } = utcNow;
		public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
	}

	private sealed class FakeUnitOfWork : IUnitOfWork
	{
		public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult(1);
	}

	private sealed class FakeCreditCardRepository(CreditCard card) : ICreditCardRepository
	{
		public Task<IEnumerable<CreditCard>> GetByClientIdAsync(string clientId) =>
			Task.FromResult<IEnumerable<CreditCard>>([card]);

		public Task<CreditCard?> GetByIdAsync(Guid id) =>
			Task.FromResult<CreditCard?>(id == card.Id ? card : null);

		public Task<CreditCard> CreateAsync(CreditCard candidate) => Task.FromResult(candidate);
		public Task UpdateAsync(CreditCard candidate) => Task.CompletedTask;
		public Task DeleteAsync(Guid id, string clientId) => Task.CompletedTask;
	}

	private sealed class FakePaymentRepository(Payment payment) : IPaymentRepository
	{
		public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
			Task.FromResult<Payment?>(id == payment.Id ? payment : null);

		public Task<Payment?> GetBySourceQuoteIdAsync(Guid sourceQuoteId, CancellationToken cancellationToken = default) =>
			Task.FromResult<Payment?>(payment.SourceQuoteId == sourceQuoteId ? payment : null);

		public Task<IReadOnlyCollection<Payment>> GetByClientIdAsync(string clientId, int page, int pageSize, CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyCollection<Payment>>([payment]);

		public Task<int> GetCountByClientIdAsync(string clientId, CancellationToken cancellationToken = default) =>
			Task.FromResult(1);

		public Task<IReadOnlyCollection<Payment>> GetDueAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyCollection<Payment>>([payment]);

		public Task<PaymentInsertResult> InsertIfSourceQuoteAbsentAsync(Payment candidate, CancellationToken cancellationToken = default) =>
			Task.FromResult(new PaymentInsertResult(false, payment));

		public Task UpdateAsync(Payment candidate, CancellationToken cancellationToken = default) =>
			Task.CompletedTask;
	}

	private sealed class FakeCardGateway(
		List<string> calls,
		params PaymentOperationResult[] results) : ICardPaymentGateway
	{
		private readonly Queue<PaymentOperationResult> _results = new(results);
		public long? ChargedMinorUnits { get; private set; }

		public Task<string> EnsureOperatorProviderAsync(CreditCard card, CancellationToken cancellationToken = default) =>
			Task.FromResult(card.OperatorProvider);

		public Task<PaymentOperationResult> CheckReadinessAsync(CreditCard card, CancellationToken cancellationToken = default)
		{
			calls.Add("card-check");
			return Task.FromResult(_results.Dequeue());
		}

		public Task<PaymentOperationResult> ChargeAsync(Payment payment, CreditCard card, CancellationToken cancellationToken = default)
		{
			calls.Add("card-charge");
			ChargedMinorUnits = payment.TotalMinorUnits;
			return Task.FromResult(_results.Dequeue());
		}

		public Task<PaymentOperationResult> ReconcileChargeAsync(Payment payment, CancellationToken cancellationToken = default)
		{
			calls.Add("card-reconcile");
			return Task.FromResult(_results.Dequeue());
		}
	}

	private sealed class FakeCompanyGateway(
		List<string> calls,
		params PaymentOperationResult[] results) : ICompanyPaymentGateway
	{
		private readonly Queue<PaymentOperationResult> _results = new(results);
		public long? PaidMinorUnits { get; private set; }
		public long? RolledBackMinorUnits { get; private set; }

		public Task<PaymentOperationResult> PayServiceAsync(Payment payment, CancellationToken cancellationToken = default)
		{
			calls.Add("company-pay");
			PaidMinorUnits = payment.TotalMinorUnits;
			return Task.FromResult(_results.Dequeue());
		}

		public Task<PaymentOperationResult> ReconcilePaymentAsync(Payment payment, CancellationToken cancellationToken = default)
		{
			calls.Add("company-reconcile");
			return Task.FromResult(_results.Dequeue());
		}

		public Task<PaymentOperationResult> RollbackAsync(Payment payment, CancellationToken cancellationToken = default)
		{
			calls.Add("company-rollback");
			RolledBackMinorUnits = payment.TotalMinorUnits;
			return Task.FromResult(_results.Dequeue());
		}
	}
}
