using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class CreatePaymentQuoteUseCase(
		IServiceRepository serviceRepository,
		IFeeConfigurationRepository feeConfigRepository,
		IPaymentQuoteRepository paymentQuoteRepository,
		IPaymentQuotePricingService pricingService,
		IUnitOfWork unitOfWork,
		IClock clock)
	{
		private readonly IServiceRepository _serviceRepository = serviceRepository;
		private readonly IFeeConfigurationRepository _feeConfigRepository = feeConfigRepository;
		private readonly IPaymentQuoteRepository _paymentQuoteRepository = paymentQuoteRepository;
		private readonly IPaymentQuotePricingService _pricingService = pricingService;
		private readonly IUnitOfWork _unitOfWork = unitOfWork;
		private readonly IClock _clock = clock;

		public async Task<PaymentQuoteResult> ExecuteAsync(
			CreatePaymentQuoteCommand request,
			CancellationToken cancellationToken = default)
		{
			var service = await _serviceRepository.GetByIdAsync(request.ServiceId);
			if (service == null)
			{
				return new PaymentQuoteResult(false, null, "Service not found.");
			}

			if (!string.Equals(
				service.Currency,
				request.Currency,
				StringComparison.OrdinalIgnoreCase))
			{
				return new PaymentQuoteResult(
					false,
					null,
					"Currency is not valid for this service.");
			}

			var fees = (await _feeConfigRepository.GetActiveFeesAsync()).ToList();
			var details = _pricingService.Calculate(service, fees, request.Currency);
			var totalAmount = details.Sum(d => d.Amount);

			var quote = PaymentQuote.Create(
				request.ClientId,
				request.ServiceId,
				request.Currency,
				totalAmount,
				_clock.UtcNow.AddMinutes(60),
				_clock.UtcNow);

			foreach (var detail in details)
			{
				quote.Details.Add(PaymentQuoteDetail.Create(
					quote.Id,
					detail.Type,
					detail.Description,
					detail.Amount,
					detail.Currency));
			}

			await _paymentQuoteRepository.CreateAsync(quote);
			await _unitOfWork.SaveChangesAsync(cancellationToken);

			return new PaymentQuoteResult(true, quote, null);
		}
	}
}
