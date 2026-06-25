using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class GetPaymentUseCase(IPaymentRepository paymentRepository)
	{
		private readonly IPaymentRepository _paymentRepository = paymentRepository;

		public async Task<Payment?> ExecuteAsync(string clientId, Guid paymentId)
		{
			var payment = await _paymentRepository.GetByIdAsync(paymentId);
			if (payment == null || payment.ClientId != clientId)
			{
				return null;
			}

			return payment;
		}
	}
}
