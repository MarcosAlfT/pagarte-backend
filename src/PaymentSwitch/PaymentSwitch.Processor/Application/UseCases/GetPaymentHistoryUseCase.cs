using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class GetPaymentHistoryUseCase(IPaymentRepository paymentRepository)
	{
		private readonly IPaymentRepository _paymentRepository = paymentRepository;

		public async Task<PaymentHistoryResult> ExecuteAsync(
			string clientId,
			int page,
			int pageSize)
		{
			var payments = await _paymentRepository.GetByClientIdAsync(
				clientId,
				page,
				pageSize);
			var total = await _paymentRepository.GetCountByClientIdAsync(clientId);

			return new PaymentHistoryResult(payments.ToList(), total);
		}
	}

	public sealed record PaymentHistoryResult(
		IReadOnlyCollection<Payment> Payments,
		int Total);
}
