using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface ICardRegistrationRepository
{
	Task<CardRegistrationSession?> GetAsync(Guid id, CancellationToken cancellationToken);
	Task<CardRegistrationSession?> FindCreationAsync(string clientId, string key, CancellationToken cancellationToken);
	Task<CardRegistrationSession?> FindCompletionAsync(string clientId, string key, CancellationToken cancellationToken);
	Task<CardRegistrationSession?> FindTokenAsync(string digest, CancellationToken cancellationToken);
	Task<IReadOnlyList<Guid>> GetDueAsync(DateTime utcNow, CancellationToken cancellationToken);
	void Add(CardRegistrationSession session);
	Task<bool> HasNotificationAsync(string integrationId, string eventId, CancellationToken cancellationToken);
	void AddNotification(CardSetupNotification notification);
}

// Application use cases own commits. Acceptance, removal and defaults share one owner lock.
public interface ICardMutationScope
{
	Task<ICardMutationTransaction> BeginAsync(string clientId, CancellationToken cancellationToken);
}
public interface ICardMutationTransaction : IAsyncDisposable
{
	Task CommitAsync(CancellationToken cancellationToken);
}
public interface IRegistrationTokenProtector
{
	void EnsureConfigured();
	string Digest(string scope, string token);
	string Protect(string token);
	string Unprotect(string protectedToken);
}
