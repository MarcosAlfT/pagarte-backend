using Microsoft.EntityFrameworkCore;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure.Repository;

public sealed class CardRegistrationRepository(PaymentDbContext db) : ICardRegistrationRepository
{
	public Task<CardRegistrationSession?> GetAsync(Guid id, CancellationToken ct) => db.CardRegistrationSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
	public Task<CardRegistrationSession?> FindCreationAsync(string clientId, string key, CancellationToken ct) => db.CardRegistrationSessions.FirstOrDefaultAsync(s => s.ClientId == clientId && s.CreationKey == key, ct);
	public Task<CardRegistrationSession?> FindCompletionAsync(string clientId, string key, CancellationToken ct) => db.CardRegistrationSessions.FirstOrDefaultAsync(s => s.ClientId == clientId && s.CompletionKey == key, ct);
	public Task<CardRegistrationSession?> FindTokenAsync(string digest, CancellationToken ct) => db.CardRegistrationSessions.FirstOrDefaultAsync(s => s.TokenDigest == digest, ct);
	public async Task<IReadOnlyList<Guid>> GetDueAsync(DateTime now, CancellationToken ct) => await db.CardRegistrationSessions.AsNoTracking()
		.Where(s => s.NextAttemptAt != null && s.NextAttemptAt <= now && (s.LeaseUntil == null || s.LeaseUntil <= now))
		.OrderBy(s => s.NextAttemptAt).Take(50).Select(s => s.Id).ToArrayAsync(ct);
	public Task<bool> HasNotificationAsync(string integrationId, string eventId, CancellationToken ct) => db.CardSetupNotifications.AnyAsync(e => e.IntegrationId == integrationId && e.EventId == eventId, ct);
	public void AddNotification(CardSetupNotification notification) => db.CardSetupNotifications.Add(notification);
	public void Add(CardRegistrationSession session) => db.CardRegistrationSessions.Add(session);
}
