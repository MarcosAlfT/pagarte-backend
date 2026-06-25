using PaymentSwitch.Messaging;
using PaymentSwitch.Processor.Application.Abstractions;
using PaymentSwitch.Processor.Application.Models;
using PaymentSwitch.Processor.Domain.Entities;
using PaymentSwitch.Processor.Domain.Enums;
using PaymentSwitch.Processor.Interfaces;

namespace PaymentSwitch.Processor.Application.UseCases
{
	public sealed class ConfirmPaymentQuoteUseCase(
		IPaymentRepository paymentRepository,
		IPaymentQuoteRepository paymentQuoteRepository,
		ICreditCardRepository creditCardRepository,
		ICardAuthorizationGateway cardAuthorizationGateway,
		IPaymentRequestOutbox paymentRequestOutbox,
		IUnitOfWork unitOfWork,
		IClock clock,
		ILogger<ConfirmPaymentQuoteUseCase> logger)
	{
		private readonly IPaymentRepository _paymentRepository = paymentRepository;
		private readonly IPaymentQuoteRepository _paymentQuoteRepository = paymentQuoteRepository;
		private readonly ICreditCardRepository _creditCardRepository = creditCardRepository;
		private readonly ICardAuthorizationGateway _cardAuthorizationGateway =
			cardAuthorizationGateway;
		private readonly IPaymentRequestOutbox _paymentRequestOutbox = paymentRequestOutbox;
		private readonly IUnitOfWork _unitOfWork = unitOfWork;
		private readonly IClock _clock = clock;
		private readonly ILogger<ConfirmPaymentQuoteUseCase> _logger = logger;

		public async Task<PaymentResult> ExecuteAsync(
			ConfirmPaymentCommand request,
			CancellationToken cancellationToken = default)
		{
			var quote = await _paymentQuoteRepository.GetByIdAsync(request.QuoteId);
			if (quote == null || quote.ClientId != request.ClientId)
			{
				return new PaymentResult(false, null, null, null, "Quote not found.");
			}

			if (quote.Status == PaymentQuoteStatus.Paid)
			{
				return new PaymentResult(false, null, null, null, "Quote was already paid.");
			}

			if (quote.IsExpired(_clock.UtcNow))
			{
				return new PaymentResult(false, null, null, null, "Quote expired.");
			}

			var card = await _creditCardRepository.GetByIdAsync(request.CreditCardId);
			if (card == null || card.ClientId != request.ClientId)
			{
				return new PaymentResult(false, null, null, null, "Credit card not found.");
			}

			var existingOperatorProvider = card.OperatorProvider;
			var operatorProvider =
				await _cardAuthorizationGateway.EnsureOperatorProviderAsync(card);

			if (!string.Equals(
				existingOperatorProvider,
				card.OperatorProvider,
				StringComparison.Ordinal))
			{
				await _creditCardRepository.UpdateAsync(card);
			}

			var payment = Payment.Create(
				request.ClientId,
				quote.Id,
				request.CreditCardId,
				quote.ServiceId,
				quote.Currency,
				operatorProvider,
				_clock.UtcNow);

			foreach (var detail in quote.Details)
			{
				payment.Details.Add(PaymentDetail.Create(
					payment.Id,
					detail.Type,
					detail.Description,
					detail.Amount,
					detail.Currency));
			}

			await _paymentRepository.CreateAsync(payment);

			payment.UpdateStatus(
				PaymentTransactionStatus.ChargingCard,
				_clock.UtcNow);
			await _paymentRepository.UpdateAsync(payment);
			await _unitOfWork.SaveChangesAsync(cancellationToken);

			var chargeResult = await _cardAuthorizationGateway.AuthorizeAsync(
				operatorProvider,
				card.OperatorCardToken,
				quote.TotalAmount,
				quote.Currency,
				payment.Reference);

			if (!chargeResult.Success)
			{
				payment.UpdateStatus(
					PaymentTransactionStatus.Failed,
					_clock.UtcNow,
					chargeResult.ErrorMessage);
				await _paymentRepository.UpdateAsync(payment);
				await _unitOfWork.SaveChangesAsync(cancellationToken);

				_logger.LogWarning(
					"Payment operator charge failed for payment {Reference}: {Error}",
					payment.Reference,
					chargeResult.ErrorMessage);

				return new PaymentResult(
					false,
					payment.Id,
					payment.Reference,
					PaymentTransactionStatus.Failed.ToString(),
					chargeResult.ErrorMessage);
			}

			quote.MarkPaid(_clock.UtcNow);

			payment.SetOperatorPaymentId(
				chargeResult.OperatorPaymentId!,
				_clock.UtcNow);
			payment.UpdateStatus(
				PaymentTransactionStatus.CardCharged,
				_clock.UtcNow);

			await _paymentQuoteRepository.UpdateAsync(quote);
			await _paymentRepository.UpdateAsync(payment);
			_paymentRequestOutbox.AddPaymentRequest(payment, quote, request.ClientId);
			await _unitOfWork.SaveChangesAsync(cancellationToken);

			_logger.LogInformation(
				"Payment {Reference} charged, queued for Engine publishing",
				payment.Reference);

			return new PaymentResult(
				true,
				payment.Id,
				payment.Reference,
				PaymentTransactionStatus.CardCharged.ToString(),
				null);
		}
	}
}
