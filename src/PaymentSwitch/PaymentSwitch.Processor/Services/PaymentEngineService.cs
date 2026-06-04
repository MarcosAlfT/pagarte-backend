using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Infrastructure;
using PaymentSwitch.Processor.Interfaces;
using System.Text.Json;

namespace PaymentSwitch.Processor.Services
{
	public record PaymentResult(
		bool Success,
		Guid? PaymentId,
		string? Reference,
		string? Status,
		string? ErrorMessage);

	public record PaymentQuoteResult(
		bool Success,
		PaymentQuote? Quote,
		string? ErrorMessage);

	public class PaymentEngineService(
		IPaymentRepository paymentRepository,
		IPaymentQuoteRepository paymentQuoteRepository,
		ICreditCardRepository creditCardRepository,
		IServiceRepository serviceRepository,
		IFeeConfigurationRepository feeConfigRepository,
		IPaymentOperatorResolver paymentOperatorResolver,
		PaymentDbContext dbContext,
		ILogger<PaymentEngineService> logger)
	{
		private readonly IPaymentRepository _paymentRepository = paymentRepository;
		private readonly IPaymentQuoteRepository _paymentQuoteRepository = paymentQuoteRepository;
		private readonly ICreditCardRepository _creditCardRepository = creditCardRepository;
		private readonly IServiceRepository _serviceRepository = serviceRepository;
		private readonly IFeeConfigurationRepository _feeConfigRepository = feeConfigRepository;
		private readonly IPaymentOperatorResolver _paymentOperatorResolver = paymentOperatorResolver;
		private readonly PaymentDbContext _dbContext = dbContext;
		private readonly ILogger<PaymentEngineService> _logger = logger;

		public async Task<PaymentQuoteResult> CreateQuoteAsync(
			string clientId, Guid serviceId, string currency)
		{
			var service = await _serviceRepository.GetByIdAsync(serviceId);
			if (service == null)
				return new PaymentQuoteResult(false, null, "Service not found.");

			if (!string.Equals(service.Currency, currency, StringComparison.OrdinalIgnoreCase))
				return new PaymentQuoteResult(false, null, "Currency is not valid for this service.");

			var fees = (await _feeConfigRepository.GetActiveFeesAsync()).ToList();
			var details = CalculateDetails(service, fees, currency);
			var totalAmount = details.Sum(d => d.Amount);

			var quote = PaymentQuote.Create(
				clientId, serviceId, currency, totalAmount, DateTime.UtcNow.AddMinutes(60));

			foreach (var detail in details)
			{
				quote.Details.Add(PaymentQuoteDetail.Create(
					quote.Id, detail.Type, detail.Description,
					detail.Amount, currency));
			}

			await _paymentQuoteRepository.CreateAsync(quote);
			return new PaymentQuoteResult(true, quote, null);
		}

		public async Task<PaymentResult> ConfirmAsync(
			string clientId, Guid quoteId, Guid creditCardId)
		{
			var quote = await _paymentQuoteRepository.GetByIdAsync(quoteId);
			if (quote == null || quote.ClientId != clientId)
				return new PaymentResult(false, null, null, null, "Quote not found.");

			if (quote.Status == PaymentQuoteStatus.Paid)
				return new PaymentResult(false, null, null, null, "Quote was already paid.");

			if (quote.IsExpired(DateTime.UtcNow))
				return new PaymentResult(false, null, null, null, "Quote expired.");

			var card = await _creditCardRepository.GetByIdAsync(creditCardId);
			if (card == null || card.ClientId != clientId)
				return new PaymentResult(false, null, null, null, "Credit card not found.");

			var operatorProvider = card.OperatorProvider;
			var paymentOperator = string.IsNullOrWhiteSpace(operatorProvider)
				? await _paymentOperatorResolver.ResolveInternationalAsync()
				: null;

			if (paymentOperator != null)
			{
				operatorProvider = paymentOperator.ProviderCode;
				card.OperatorProvider = operatorProvider;
				await _creditCardRepository.UpdateAsync(card);
			}

			var adapter = paymentOperator?.Adapter
				?? _paymentOperatorResolver.GetAdapter(operatorProvider);

			var payment = Payment.Create(
				clientId, quote.Id, creditCardId, quote.ServiceId,
				quote.Currency, operatorProvider);

			foreach (var detail in quote.Details)
			{
				payment.Details.Add(PaymentDetail.Create(
					payment.Id, detail.Type, detail.Description,
					detail.Amount, detail.Currency));
			}

			await _paymentRepository.CreateAsync(payment);

			payment.UpdateStatus(TransactionStatus.ChargingCard);
			await _paymentRepository.UpdateAsync(payment);

			var chargeResult = await adapter.ChargeAsync(
				card.OperatorCardToken, quote.TotalAmount, quote.Currency, payment.Reference);

			if (!chargeResult.Success)
			{
				payment.UpdateStatus(TransactionStatus.Failed, chargeResult.ErrorMessage);
				await _paymentRepository.UpdateAsync(payment);

				_logger.LogWarning("Payment operator charge failed for payment {Reference}: {Error}",
					payment.Reference, chargeResult.ErrorMessage);

				return new PaymentResult(false, payment.Id, payment.Reference,
					"Failed", chargeResult.ErrorMessage);
			}

			quote.MarkPaid();

			payment.SetOperatorPaymentId(chargeResult.OperatorPaymentId!);
			payment.UpdateStatus(TransactionStatus.CardCharged);

			var paymentRequestMessage = new PaymentRequestMessage
			{
				PaymentId = payment.Id,
				CompanyId = quote.Service.CompanyId,
				OperatorProvider = payment.OperatorProvider,
				OperatorPaymentId = chargeResult.OperatorPaymentId!,
				Amount = quote.TotalAmount,
				Currency = quote.Currency,
				Reference = payment.Reference,
				ClientId = clientId
			};

			var outboxMessage = OutboxMessage.Create(
				typeof(PaymentRequestMessage).FullName ?? nameof(PaymentRequestMessage),
				JsonSerializer.Serialize(paymentRequestMessage),
				PaymentSwitchQueues.Exchanges.Payments,
				PaymentSwitchQueues.Queues.PaymentRequest);

			_dbContext.PaymentQuotes.Update(quote);
			_dbContext.Payments.Update(payment);
			_dbContext.OutboxMessages.Add(outboxMessage);
			await _dbContext.SaveChangesAsync();

			_logger.LogInformation("Payment {Reference} charged, queued for Engine publishing",
				payment.Reference);

			return new PaymentResult(true, payment.Id, payment.Reference,
				"CardCharged", null);
		}

		private static List<(PaymentDetailType Type, string Description,
			decimal Amount)> CalculateDetails(
			Service service, List<FeeConfiguration> fees, string currency)
		{
			var details = new List<(PaymentDetailType, string, decimal)>
			{
				(PaymentDetailType.ServiceAmount, service.Name, service.BaseAmount)
			};

			foreach (var fee in fees)
			{
				var amount = fee.CalculationType == CalculationType.Percentage
					? service.BaseAmount * fee.Value / 100
					: fee.Value;

				var type = fee.Type switch
				{
					FeeType.PaymentOperator => PaymentDetailType.PaymentOperatorFee,
					FeeType.Company => PaymentDetailType.CompanyFee,
					FeeType.Platform => PaymentDetailType.PlatformFee,
					_ => PaymentDetailType.Tax
				};

				details.Add((type, $"{fee.Type} Fee", amount));
			}

			return details;
		}
	}
}
