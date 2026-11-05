using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure.Repository;

public sealed class CardDetachmentRepository(PaymentDbContext db) : ICardDetachmentRepository
{
	public Task<CreditCard?> GetAsync(Guid id, CancellationToken ct) => db.CreditCards.FirstOrDefaultAsync(c => c.Id == id, ct);
	public async Task<IReadOnlyList<Guid>> GetDueAsync(DateTime now, CancellationToken ct) => await db.CreditCards.AsNoTracking()
		.Where(c => c.IsDeleted && c.DetachmentNextAttemptAt != null && c.DetachmentNextAttemptAt <= now
			&& (c.DetachmentLeaseUntil == null || c.DetachmentLeaseUntil <= now))
		.OrderBy(c => c.DetachmentNextAttemptAt).Take(50).Select(c => c.Id).ToArrayAsync(ct);
	public Task<bool> HasRegistrationBarrierAsync(string owner, CancellationToken ct) => db.CreditCards.AnyAsync(c =>
		c.ClientId == owner && c.IsDeleted && c.DetachmentAttempts > 0 && (c.DetachmentState == "Pending" || c.DetachmentState == "RequiresReview"), ct);

	public async Task<bool> HasDependenciesAsync(Guid cardId, bool refundsSurviveDetachment, CancellationToken ct)
	{
		var card = await db.CreditCards.SingleAsync(c => c.Id == cardId, ct);
		var matching = db.CreditCards.Where(c => c.AdapterProvider == card.AdapterProvider && c.ProviderAccount == card.ProviderAccount
			&& c.ProviderEnvironment == card.ProviderEnvironment && c.OperatorCardToken == card.OperatorCardToken);
		if (await matching.AnyAsync(c => c.Id != cardId && !c.IsDeleted, ct)) return true;
		if (await db.CardRegistrationSessions.AnyAsync(s => s.ClientId == card.ClientId && s.Provider == card.AdapterProvider
			&& s.ProviderAccount == card.ProviderAccount && s.ProviderEnvironment == card.ProviderEnvironment
			&& (s.State == CardRegistrationState.Pending || s.State == CardRegistrationState.Verifying
				|| s.State == CardRegistrationState.RequiresAction || s.State == CardRegistrationState.Unresolved
				|| s.CleanupState == "Pending" || s.CleanupState == "RequiresReview"), ct)) return true;
		return await db.Payments.AnyAsync(p => matching.Select(c => c.Id).Contains(p.CreditCardId) && (!refundsSurviveDetachment ||
			(p.Status != PaymentTransactionStatus.Completed && p.Status != PaymentTransactionStatus.Failed
				&& p.Status != PaymentTransactionStatus.CompanyRolledBack && p.Status != PaymentTransactionStatus.Refunded
				&& p.Status != PaymentTransactionStatus.Compensated)), ct);
	}
}
