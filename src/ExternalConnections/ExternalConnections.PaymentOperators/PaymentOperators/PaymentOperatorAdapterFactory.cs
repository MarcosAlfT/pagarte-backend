using Microsoft.Extensions.DependencyInjection;
using ExternalConnections.PaymentOperators.Config;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

public sealed class PaymentOperatorAdapterFactory(IServiceProvider serviceProvider, PaymentOperatorRegistry registry, PaymentOperatorRouteGuard routes)
    : IPaymentOperatorAdapterFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public IPaymentOperatorAdapter GetRequiredAdapter(string providerCode)
    {
        var integrationId = providerCode;
        OperatorIntegration? integration = null;
        if (integrationId.Equals("Mock", StringComparison.OrdinalIgnoreCase) || integrationId.Equals("DLocal", StringComparison.OrdinalIgnoreCase))
        {
            providerCode = registry.ResolveAdapter(integrationId);
        }
        else
        {
            integration = registry.GetRequired(integrationId);
            providerCode = integration.Adapter;
            routes.Validate(new(integrationId, integration.Adapter, integration.Account, integration.Environment, integration.Version, integration.CredentialReference));

        }
        if (providerCode.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            if (integrationId.Equals("Mock", StringComparison.OrdinalIgnoreCase)) return _serviceProvider.GetRequiredService<MockPaymentOperatorAdapter>();
            return ActivatorUtilities.CreateInstance<MockPaymentOperatorAdapter>(_serviceProvider,
                new OperatorCardIdentity(integrationId, integration!.Adapter, integration.Account, integration.Environment, integration.Version, integration.CredentialReference));
        }

        if (providerCode.Equals("DLocal", StringComparison.OrdinalIgnoreCase))
        {
            if (integration is null) return _serviceProvider.GetRequiredService<DLocalPaymentOperatorAdapter>();
            return ActivatorUtilities.CreateInstance<DLocalPaymentOperatorAdapter>(_serviceProvider,
                new OperatorCardIdentity(integrationId, integration.Adapter, integration.Account, integration.Environment, integration.Version, integration.CredentialReference));
        }

        throw new InvalidOperationException(
            $"Payment operator provider '{providerCode}' is not supported.");
    }
}
