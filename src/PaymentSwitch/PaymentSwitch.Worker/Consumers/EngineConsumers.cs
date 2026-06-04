using ExternalConnections.CompanyPayments;
using ExternalConnections.PaymentOperators.PaymentOperators;
using PaymentSwitch.Worker.Interfaces;
using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;
using Infrastructure.RabbitMQ;

namespace PaymentSwitch.Worker.Consumers
{
    /// <summary>
    /// Reads PaymentRequestMessage published by Worker after payment operator charge.
    /// Calls company API. On failure triggers refund.
    /// </summary>
    public class PaymentRequestConsumer(
        RabbitMqConnectionFactory connectionFactory,
        ICompanyAdapter companyAdapter,
        IPaymentStatusRepository paymentStatus,
        IMessagePublisher messagePublisher,
        ILogger<PaymentRequestConsumer> logger)
        : BaseConsumer<PaymentRequestMessage>(connectionFactory, logger)
    {
        private readonly ICompanyAdapter _companyAdapter = companyAdapter;
        private readonly IPaymentStatusRepository _paymentStatus = paymentStatus;
        private readonly IMessagePublisher _messagePublisher = messagePublisher;

        protected override string QueueName => PaymentSwitchQueues.Queues.PaymentRequest;

        protected override async Task HandleAsync(
            PaymentRequestMessage message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing payment request {PaymentId}", message.PaymentId);

            await _paymentStatus.UpdateStatusAsync(
                message.PaymentId, "SendingPaymentToCompany");

            // Get company details from payment info
            var info = await _paymentStatus.GetPaymentInfoAsync(message.PaymentId);

            var result = await _companyAdapter.SendPaymentAsync(
                $"company_{message.CompanyId}", // resolved from DB in real impl
                string.Empty,
                message.Amount,
                message.Currency,
                message.Reference,
                message.ClientId);

            if (result.Success)
            {
                await _paymentStatus.UpdateStatusAsync(
                    message.PaymentId, "Completed",
                    companyReference: result.CompanyReference);

                // Send success email
                await _messagePublisher.PublishAsync(
                    new EmailMessage
                    {
                        To = message.ClientId,
                        Subject = "Payment completed",
                        Body = $"Your payment {message.Reference} has been completed successfully."
                    },
                    PaymentSwitchQueues.Exchanges.Notifications,
                    PaymentSwitchQueues.Queues.EmailSend);

                _logger.LogInformation("Payment {Reference} completed", message.Reference);
            }
            else
            {
                _logger.LogWarning("Company rejected payment {Reference}: {Error}",
                    message.Reference, result.ErrorMessage);

                await _paymentStatus.UpdateStatusAsync(
                    message.PaymentId, "CompanyPaymentFailed", errorMessage: result.ErrorMessage);

                await _paymentStatus.UpdateStatusAsync(
                    message.PaymentId, "Refunding");

                // Trigger refund
                await _messagePublisher.PublishAsync(
                    new RefundRequestMessage
                    {
                        PaymentId = message.PaymentId,
                        OperatorProvider = message.OperatorProvider,
                        OperatorPaymentId = message.OperatorPaymentId,
                        Amount = message.Amount,
                        Currency = message.Currency,
                        Reason = result.ErrorMessage ?? "Company rejected payment",
                        RetryCount = 0
                    },
                    PaymentSwitchQueues.Exchanges.Payments,
                    PaymentSwitchQueues.Queues.RefundRequest);
            }
        }
    }

    /// <summary>
    /// Handles refund requests with retry logic Ã¢â‚¬â€ 3 attempts every 5 minutes.
    /// </summary>
    public class RefundConsumer(
        RabbitMqConnectionFactory connectionFactory,
        IPaymentOperatorAdapterFactory paymentOperatorAdapterFactory,
        IPaymentStatusRepository paymentStatus,
        IMessagePublisher messagePublisher,
        ILogger<RefundConsumer> logger)
        : BaseConsumer<RefundRequestMessage>(connectionFactory, logger)
    {
        private readonly IPaymentOperatorAdapterFactory _paymentOperatorAdapterFactory = paymentOperatorAdapterFactory;
        private readonly IPaymentStatusRepository _paymentStatus = paymentStatus;
        private readonly IMessagePublisher _messagePublisher = messagePublisher;
        private const int MaxRetries = 3;

        protected override string QueueName => PaymentSwitchQueues.Queues.RefundRequest;

        protected override async Task HandleAsync(
            RefundRequestMessage message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing refund for payment {PaymentId}, attempt {Retry}",
                message.PaymentId, message.RetryCount + 1);

            if (string.IsNullOrWhiteSpace(message.OperatorProvider))
            {
                throw new InvalidOperationException(
                    "Refund request does not contain an operator provider.");
            }

            var paymentOperatorAdapter = _paymentOperatorAdapterFactory.GetRequiredAdapter(
                message.OperatorProvider);

            var result = await paymentOperatorAdapter.RefundAsync(
                message.OperatorPaymentId,
                message.Amount,
                message.Currency,
                message.Reason);

            if (result.Success)
            {
                await _paymentStatus.UpdateStatusAsync(
                    message.PaymentId, "Refunded");

                await _messagePublisher.PublishAsync(
                    new EmailMessage
                    {
                        To = string.Empty, // resolved from payment
                        Subject = "Payment refunded",
                        Body = $"Your payment has been refunded. Reason: {message.Reason}"
                    },
                    PaymentSwitchQueues.Exchanges.Notifications,
                    PaymentSwitchQueues.Queues.EmailSend);

                _logger.LogInformation("Refund completed for payment {PaymentId}",
                    message.PaymentId);
            }
            else if (message.RetryCount < MaxRetries - 1)
            {
                _logger.LogWarning("Refund attempt {Retry}/{Max} failed for {PaymentId}",
                    message.RetryCount + 1, MaxRetries, message.PaymentId);

                await _paymentStatus.IncrementRetryAsync(message.PaymentId);

                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                await _messagePublisher.PublishAsync(
                    new RefundRequestMessage
                    {
                        PaymentId = message.PaymentId,
                        OperatorProvider = message.OperatorProvider,
                        OperatorPaymentId = message.OperatorPaymentId,
                        Amount = message.Amount,
                        Currency = message.Currency,
                        Reason = message.Reason,
                        RetryCount = message.RetryCount + 1
                    },
                    PaymentSwitchQueues.Exchanges.Payments,
                    PaymentSwitchQueues.Queues.RefundRequest);
            }
            else
            {
                _logger.LogError("Refund failed after {Max} attempts for payment {PaymentId}",
                    MaxRetries, message.PaymentId);

                await _paymentStatus.UpdateStatusAsync(
                    message.PaymentId, "RefundFailed",
                    errorMessage: $"Refund failed after {MaxRetries} attempts");

                await _messagePublisher.PublishAsync(
                    new AlertMessage
                    {
                        PaymentId = message.PaymentId,
                        Message = $"URGENT: Refund failed after {MaxRetries} attempts for payment {message.PaymentId}. Manual intervention required.",
                        Severity = "Critical"
                    },
                    PaymentSwitchQueues.Exchanges.Notifications,
                    PaymentSwitchQueues.Queues.AlertCreate);
            }
        }
    }

    /// <summary>
    /// Sends emails via SMTP Ã¢â‚¬â€ fire and forget.
    /// </summary>
    public class EmailConsumer(
        RabbitMqConnectionFactory connectionFactory,
        IEmailSenderService emailSender,
        ILogger<EmailConsumer> logger)
        : BaseConsumer<EmailMessage>(connectionFactory, logger)
    {
        private readonly IEmailSenderService _emailSender = emailSender;

        protected override string QueueName => PaymentSwitchQueues.Queues.EmailSend;

        protected override async Task HandleAsync(
            EmailMessage message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(message.To))
            {
                _logger.LogWarning("Email skipped Ã¢â‚¬â€ no recipient");
                return;
            }

            await _emailSender.SendAsync(
                message.To, message.Subject, message.Body, message.IsHtml);
        }
    }
}
