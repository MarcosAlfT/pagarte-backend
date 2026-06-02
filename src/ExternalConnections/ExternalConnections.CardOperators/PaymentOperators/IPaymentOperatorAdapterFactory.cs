namespace ExternalConnections.CardOperators.PaymentOperators;

public interface IPaymentOperatorAdapterFactory
{
    IPaymentOperatorAdapter GetRequiredAdapter(string providerCode);
}
