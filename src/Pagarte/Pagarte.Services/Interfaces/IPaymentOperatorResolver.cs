using ExternalConnections.CardOperators.PaymentOperators;

namespace Pagarte.Services.Interfaces
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
