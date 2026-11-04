using PaymentSwitch.Processor.Application.Models;

namespace PaymentSwitch.Processor.Application.Abstractions;

public interface ICreditCardRegistrationGateway
{
	VerifiedCardSetupNotification? VerifyNotification(string integrationId, IReadOnlyDictionary<string, string> headers, byte[] payload, DateTime utcNow);
	CardOperatorIdentity SelectOperator();
	Task<CardRemovalOutcome> GetRemovalStatusAsync(CardOperatorIdentity identity, string reference, CancellationToken cancellationToken);
	CardRemovalCapabilities GetRemovalCapabilities(CardOperatorIdentity identity);
	Task<CardRemovalOutcome> RemoveCardAsync(CardOperatorIdentity identity, string reference, string operationKey, CancellationToken cancellationToken);
	Task<CardRemovalOutcome> RemoveSetupResourceAsync(CardSetupContext context, CancellationToken cancellationToken);
	CardMobileInitialization GetInitialization(CardOperatorIdentity identity);
	Task<CardSetupResult> SaveAsync(CardSetupContext context, string token, CancellationToken cancellationToken);
	Task<CardSetupResult> GetStatusAsync(CardSetupContext context, CancellationToken cancellationToken);
}
