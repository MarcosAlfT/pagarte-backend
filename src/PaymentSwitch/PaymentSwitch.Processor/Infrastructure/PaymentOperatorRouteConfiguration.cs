using ExternalConnections.PaymentOperators.Config;
using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Domain.Entities;

namespace PaymentSwitch.Processor.Infrastructure;

public sealed class PaymentOperatorRouteConfiguration(PaymentOperatorRegistry registry, PaymentOperatorRouteGuard guard) : IPaymentOperatorRouteConfiguration
{
	public IReadOnlyList<PaymentOperatorRoute> GetConfigured() => registry.GetConfigured().Select(r => new PaymentOperatorRoute {
		IntegrationId = r.Id, CredentialReference = r.Integration.CredentialReference, Provider = r.Integration.Adapter, Account = r.Integration.Account,
		Environment = r.Integration.Environment, Version = r.Integration.Version }).ToArray();
	public void Activate(IReadOnlyList<PaymentOperatorRoute> pinned) => guard.Initialize(pinned.Select(r =>
		new OperatorCardIdentity(r.IntegrationId, r.Provider, r.Account, r.Environment, r.Version, r.CredentialReference)));
}
