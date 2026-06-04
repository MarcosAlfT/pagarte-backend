using ExternalConnections.PaymentOperators.PaymentOperators;

namespace PaymentSwitch.Processor.Interfaces
{
	public interface IPaymentOperatorResolver
	{
		Task<ResolvedPaymentOperator> ResolveInternationalAsync();
		IPaymentOperatorAdapter GetAdapter(string providerCode);
	}

	public record ResolvedPaymentOperator(
		string ProviderCode,
		IPaymentOperatorAdapter Adapter);
}
