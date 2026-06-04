namespace ExternalConnections.PaymentOperators.PaymentOperators;

public interface IPaymentOperatorAdapterFactory
{
    IPaymentOperatorAdapter GetRequiredAdapter(string providerCode);
}
