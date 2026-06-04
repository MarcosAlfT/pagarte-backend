using Microsoft.Extensions.DependencyInjection;

namespace ExternalConnections.PaymentOperators.PaymentOperators;

public sealed class PaymentOperatorAdapterFactory(IServiceProvider serviceProvider)
    : IPaymentOperatorAdapterFactory
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public IPaymentOperatorAdapter GetRequiredAdapter(string providerCode)
    {
        if (providerCode.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            return _serviceProvider.GetRequiredService<MockPaymentOperatorAdapter>();
        }

        if (providerCode.Equals("DLocal", StringComparison.OrdinalIgnoreCase))
        {
            return _serviceProvider.GetRequiredService<DLocalPaymentOperatorAdapter>();
        }

        throw new InvalidOperationException(
            $"Payment operator provider '{providerCode}' is not supported.");
    }
}
