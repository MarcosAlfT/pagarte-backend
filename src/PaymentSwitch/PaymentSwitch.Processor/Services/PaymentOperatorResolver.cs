using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Services
{
	public class PaymentOperatorResolver(
		IPaymentOperatorRepository paymentOperatorRepository,
		IPaymentOperatorAdapterFactory adapterFactory)
		: IPaymentOperatorResolver
	{
		private readonly IPaymentOperatorRepository _paymentOperatorRepository = paymentOperatorRepository;
		private readonly IPaymentOperatorAdapterFactory _adapterFactory = adapterFactory;

		public async Task<ResolvedPaymentOperator> ResolveInternationalAsync()
		{
			var paymentOperator = await _paymentOperatorRepository.GetActiveAsync(
				PaymentOperatorScope.International);

			if (paymentOperator == null)
			{
				throw new InvalidOperationException(
					"No active international payment operator is configured.");
			}

			return new ResolvedPaymentOperator(
				paymentOperator.Code,
				_adapterFactory.GetRequiredAdapter(paymentOperator.Code));
		}

		public IPaymentOperatorAdapter GetAdapter(string providerCode)
			=> _adapterFactory.GetRequiredAdapter(providerCode);
	}
}
