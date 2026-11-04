using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure.Repository;

public sealed class CardReviewRepository(PaymentDbContext db) : ICardReviewRepository
{
	public async Task<IReadOnlyList<CardReviewItem>> ListAsync(int limit, CancellationToken ct)
	{
		var registrations = await db.CardRegistrationSessions.AsNoTracking().Where(s => s.State == CardRegistrationState.Unresolved)
			.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).Take(limit)
			.Select(s => new CardReviewItem(s.Id, "Registration", "Unresolved", s.ClientId, s.Attempts, s.CreatedAt)).ToArrayAsync(ct);
		var cleanup = await db.CardRegistrationSessions.AsNoTracking().Where(s => s.CleanupState == "RequiresReview")
			.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).Take(limit)
			.Select(s => new CardReviewItem(s.Id, "SetupCleanup", s.CleanupState, s.ClientId, s.CleanupAttempts, s.CreatedAt)).ToArrayAsync(ct);
		var cards = await db.CreditCards.AsNoTracking().Where(c => c.IsDeleted && c.DetachmentState == "RequiresReview")
			.OrderBy(c => c.DeletedAt).ThenBy(c => c.Id).Take(limit)
			.Select(c => new CardReviewItem(c.Id, "CardRemoval", c.DetachmentState, c.ClientId, c.DetachmentAttempts, c.DeletedAt ?? c.CreatedAt)).ToArrayAsync(ct);
		return registrations.Concat(cleanup).Concat(cards).OrderBy(r => r.CreatedAt).ThenBy(r => r.Kind).ThenBy(r => r.Id).Take(limit).ToArray();
	}
	public Task<CardReviewOperation?> FindAsync(string actor, string key, CancellationToken ct) => db.CardReviewOperations.FirstOrDefaultAsync(r => r.Actor == actor && r.RequestKey == key, ct);
	public Task<CardReviewOperation?> GetAsync(Guid id, CancellationToken ct) => db.CardReviewOperations.FirstOrDefaultAsync(r => r.Id == id, ct);
	public void Add(CardReviewOperation operation) => db.CardReviewOperations.Add(operation);
}
