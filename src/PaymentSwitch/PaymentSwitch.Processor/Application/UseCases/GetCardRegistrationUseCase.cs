using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Application.Services;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Application.UseCases;

public sealed class GetCardRegistrationUseCase(ICardRegistrationRepository registrations,
	ICreditCardRegistrationGateway gateway, ICardMutationScope mutations, IClock clock)
{
	public async Task<CardRegistrationView> ExecuteAsync(string clientId, Guid id, bool cancel = false, CancellationToken ct = default)
	{
		await using var transaction = await mutations.BeginAsync(clientId, ct);
		var session = await registrations.GetAsync(id, ct);
		if (session is null || session.ClientId != clientId) throw CardRegistrationRules.Missing();
		if (!session.IsTerminal && (cancel || session.ExpiresAt <= clock.UtcNow))
		{
			session.Finish(cancel ? CardRegistrationState.Cancelled : CardRegistrationState.Expired,
				cancel ? "SESSION_CANCELLED" : "SESSION_EXPIRED");
			await transaction.CommitAsync(ct);
		}
		return CardRegistrationRules.View(session, gateway);
	}
}
