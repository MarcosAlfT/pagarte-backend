using ExternalConnections.CompanyPayments;
using Infrastructure.RabbitMQ;
using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using PaymentSwitch.Worker.Interfaces;

namespace PaymentSwitch.Worker.Application.UseCases
{
	public sealed class ProcessPaymentRequestUseCase(
		ICompanyAdapter companyAdapter,
		IPaymentStatusRepository paymentStatus,
		IMessagePublisher messagePublisher,
		IClock clock,
		ILogger<ProcessPaymentRequestUseCase> logger)
	{
		private readonly ICompanyAdapter _companyAdapter = companyAdapter;
		private readonly IPaymentStatusRepository _paymentStatus = paymentStatus;
		private readonly IMessagePublisher _messagePublisher = messagePublisher;
		private readonly IClock _clock = clock;
		private readonly ILogger<ProcessPaymentRequestUseCase> _logger = logger;

		public async Task ExecuteAsync(
			PaymentRequestMessage message,
			CancellationToken cancellationToken = default)
		{
			_logger.LogInformation(
				"Processing payment request {PaymentId}",
				message.PaymentId);

			await _paymentStatus.UpdateStatusAsync(
				PaymentTransactionStatus.SendingPaymentToCompany,
				message.PaymentId);

			var result = await _companyAdapter.SendPaymentAsync(
				$"company_{message.CompanyId}",
				string.Empty,
				message.Amount,
				message.Currency,
				message.Reference,
				message.ClientId);

			if (result.Success)
			{
				await CompletePaymentAsync(message, result.CompanyReference);
				return;
			}

			await RejectPaymentAsync(message, result.ErrorMessage);
		}

		private async Task CompletePaymentAsync(
			PaymentRequestMessage message,
			string? companyReference)
		{
			await _paymentStatus.UpdateStatusAsync(
				PaymentTransactionStatus.Completed,
				message.PaymentId,
				companyReference: companyReference);

			await _messagePublisher.PublishAsync(
				new EmailMessage
				{
					To = message.ClientId,
					Subject = "Payment completed",
					Body =
						$"Your payment {message.Reference} has been completed successfully.",
					CreatedAt = _clock.UtcNow
				},
				PaymentSwitchQueues.Exchanges.Notifications,
				PaymentSwitchQueues.Queues.EmailSend);

			_logger.LogInformation(
				"Payment {Reference} completed",
				message.Reference);
		}

		private async Task RejectPaymentAsync(
			PaymentRequestMessage message,
			string? errorMessage)
		{
			_logger.LogWarning(
				"Company rejected payment {Reference}: {Error}",
				message.Reference,
				errorMessage);

			await _paymentStatus.UpdateStatusAsync(
				PaymentTransactionStatus.CompanyPaymentFailed,
				message.PaymentId,
				errorMessage: errorMessage);

			await _paymentStatus.UpdateStatusAsync(
				PaymentTransactionStatus.Refunding,
				message.PaymentId);

			await _messagePublisher.PublishAsync(
				new RefundRequestMessage
				{
					PaymentId = message.PaymentId,
					OperatorProvider = message.OperatorProvider,
					OperatorPaymentId = message.OperatorPaymentId,
					Amount = message.Amount,
					Currency = message.Currency,
					Reason = errorMessage ?? "Company rejected payment",
					RetryCount = 0,
					CreatedAt = _clock.UtcNow
				},
				PaymentSwitchQueues.Exchanges.Payments,
				PaymentSwitchQueues.Queues.RefundRequest);
		}
	}
}
