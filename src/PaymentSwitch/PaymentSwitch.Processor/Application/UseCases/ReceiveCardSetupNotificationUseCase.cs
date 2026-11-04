using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class ReceiveCardSetupNotificationUseCase(ICreditCardRegistrationGateway gateway,
	ICardRegistrationRepository registrations, ICardMutationScope mutations, IClock clock)
{
	public async Task ExecuteAsync(string integrationId, IReadOnlyDictionary<string, string> headers, byte[] payload, CancellationToken ct)
	{
		var notification = gateway.VerifyNotification(integrationId, headers, payload, clock.UtcNow);
		if (notification is null) throw new CardOperationException("INVALID_PROVIDER_CALLBACK", "Invalid provider callback.", 401);
		var session = await registrations.GetAsync(notification.SessionId, ct);
		if (session is null || session.IntegrationId != integrationId) return;
		await using var transaction = await mutations.BeginAsync(session.ClientId, ct);
		session = (await registrations.GetAsync(notification.SessionId, ct))!;
		if (session.IntegrationId != integrationId || session.IsTerminal || session.CompletionKey is null
			|| session.State == CardRegistrationState.Unresolved) return;
		if (await registrations.HasNotificationAsync(integrationId, notification.EventId, ct)) return;
		registrations.AddNotification(new CardSetupNotification
		{
			Id = Guid.NewGuid(), IntegrationId = integrationId, EventId = notification.EventId,
			SessionId = session.Id, ReceivedAt = clock.UtcNow
		});
		// A callback is a wakeup, never evidence that setup succeeded. The scheduler rechecks the provider.
		session.NextAttemptAt = session.LeaseUntil > clock.UtcNow ? session.LeaseUntil : clock.UtcNow;
		await transaction.CommitAsync(ct);
	}
}
