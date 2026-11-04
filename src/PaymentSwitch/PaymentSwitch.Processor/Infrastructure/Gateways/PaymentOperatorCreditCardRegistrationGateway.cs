using ExternalConnections.PaymentOperators.Config;
using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Infrastructure.Gateways;

public sealed class PaymentOperatorCreditCardRegistrationGateway(
	IPaymentOperatorResolver resolver, PaymentOperatorRegistry registry) : ICreditCardRegistrationGateway
{
	public VerifiedCardSetupNotification? VerifyNotification(string integrationId, IReadOnlyDictionary<string, string> headers, byte[] payload, DateTime utcNow)
	{
		try
		{
			if (resolver.GetAdapter(integrationId) is not ITokenizedCardSetupAdapter adapter) return null;
			var result = adapter.VerifySetupNotification(integrationId, headers, payload, utcNow);
			return result is null ? null : new(result.SessionId, result.EventId);
		}
		catch (InvalidOperationException) { return null; }
	}

	public async Task<CardRemovalOutcome> GetRemovalStatusAsync(CardOperatorIdentity identity, string reference, CancellationToken ct)
	{
		ValidateIdentity(identity);
		return resolver.GetAdapter(identity.IntegrationId) is IStoredCardRemovalAdapter adapter
			? Map(await adapter.GetRemovalStatusAsync(new(identity.IntegrationId, identity.Provider, identity.Account, identity.Environment, identity.Version), reference, ct))
			: CardRemovalOutcome.Unsupported;
	}

	public CardRemovalCapabilities GetRemovalCapabilities(CardOperatorIdentity identity)
	{
		if (string.IsNullOrWhiteSpace(identity.Provider) || string.IsNullOrWhiteSpace(identity.Account)) return new(false, false);
		ValidateIdentity(identity);
		if (resolver.GetAdapter(identity.IntegrationId) is not IStoredCardRemovalAdapter adapter) return new(false, false);
		var capabilities = adapter.GetRemovalCapabilities(new(identity.IntegrationId, identity.Provider, identity.Account, identity.Environment, identity.Version));
		return new(capabilities.Supported, capabilities.RefundsSurviveDetachment);
	}

	public async Task<CardRemovalOutcome> RemoveCardAsync(CardOperatorIdentity identity, string reference, string operationKey, CancellationToken ct)
	{
		if (!GetRemovalCapabilities(identity).Supported) return CardRemovalOutcome.Unsupported;
		var adapter = (IStoredCardRemovalAdapter)resolver.GetAdapter(identity.IntegrationId);
		return Map(await adapter.RemoveCardAsync(new(identity.IntegrationId, identity.Provider, identity.Account, identity.Environment, identity.Version), reference, operationKey, ct));
	}

	public async Task<CardRemovalOutcome> RemoveSetupResourceAsync(CardSetupContext context, CancellationToken ct)
	{
		if (!GetRemovalCapabilities(context.Operator).Supported) return CardRemovalOutcome.Unsupported;
		return Map(await ((IStoredCardRemovalAdapter)resolver.GetAdapter(context.Operator.IntegrationId)).RemoveSetupResourceAsync(Map(context), ct));
	}

	private void ValidateIdentity(CardOperatorIdentity identity)
	{
		var registered = registry.GetRequired(identity.IntegrationId);
		if (registered.Adapter != identity.Provider || registered.Account != identity.Account
			|| registered.Environment != identity.Environment || registered.Version != identity.Version)
			throw new InvalidOperationException("The original integration identity must remain configured for recovery.");
	}
	private static CardRemovalOutcome Map(OperatorCardRemovalOutcome result) => result switch
	{
		OperatorCardRemovalOutcome.Removed => CardRemovalOutcome.Removed,
		OperatorCardRemovalOutcome.Pending => CardRemovalOutcome.Pending,
		OperatorCardRemovalOutcome.Unsupported => CardRemovalOutcome.Unsupported,
		OperatorCardRemovalOutcome.Rejected => CardRemovalOutcome.Rejected,
		_ => CardRemovalOutcome.Unknown
	};

	public CardOperatorIdentity SelectOperator()
	{
		var selected = registry.SelectForRegistration();
		var identity = new CardOperatorIdentity(selected.Id, selected.Integration.Adapter, selected.Integration.Account,
			selected.Integration.Environment, selected.Integration.Version);
		_ = GetAdapter(identity);
		return identity;
	}

	public CardMobileInitialization GetInitialization(CardOperatorIdentity identity)
	{
		var adapter = GetAdapter(identity);
		return new(identity.IntegrationId, adapter.MobileMethod, adapter.MobileVersion, null);
	}

	public async Task<CardSetupResult> SaveAsync(CardSetupContext context, string token, CancellationToken ct) =>
		Map(await GetAdapter(context.Operator).SaveTokenAsync(Map(context), token, ct));

	public async Task<CardSetupResult> GetStatusAsync(CardSetupContext context, CancellationToken ct) =>
		Map(await GetAdapter(context.Operator).GetSetupStatusAsync(Map(context), ct));

	private ITokenizedCardSetupAdapter GetAdapter(CardOperatorIdentity identity)
	{
		ValidateIdentity(identity);
		if (resolver.GetAdapter(identity.IntegrationId) is not ITokenizedCardSetupAdapter adapter
			|| !adapter.SupportsTokenizedSaving || !adapter.SupportsSetupRecovery || adapter.IntegrationVersion != identity.Version)
			throw new InvalidOperationException("The operator does not support the required tokenized setup and recovery capabilities.");
		return adapter;
	}

	private static OperatorSetupContext Map(CardSetupContext context) => new(context.SessionId, context.ClientId,
		context.Operator.IntegrationId, context.Operator.Account, context.Operator.Environment, context.Operator.Version, context.ExpiresAt);
	private static CardSetupResult Map(OperatorSetupResult result) => new(result.Outcome switch
		{
			OperatorSetupOutcome.Succeeded => CardSetupOutcome.Succeeded,
			OperatorSetupOutcome.RequiresAction => CardSetupOutcome.RequiresAction,
			OperatorSetupOutcome.Declined => CardSetupOutcome.Declined,
			OperatorSetupOutcome.Cancelled => CardSetupOutcome.Cancelled,
			OperatorSetupOutcome.NotFound => CardSetupOutcome.NotFound,
			_ => CardSetupOutcome.Unknown
		},
		result.Card is null ? null : new(result.Card.Reference, result.Card.Last4, result.Card.CardType, result.Card.ExpiryMonth, result.Card.ExpiryYear),
		result.NextAction);
}
