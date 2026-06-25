using PaymentSwitch.Messaging;
using PaymentSwitch.Messaging.Messages;

namespace PaymentSwitch.Worker.Interfaces
{
    public interface IPaymentStatusRepository
    {
        Task UpdateStatusAsync(PaymentTransactionStatus status, Guid paymentId,
            string? companyReference = null, string? errorMessage = null);
        Task ScheduleRefundRetryAsync(Guid paymentId, DateTime nextRetryAt);
        Task<IReadOnlyCollection<RefundRequestMessage>> GetDueRefundRequestsAsync(
            DateTime utcNow,
            int maxRetries,
            int batchSize);
        Task MarkRefundRetryDispatchedAsync(Guid paymentId);
        Task<(string? OperatorPaymentId, decimal Amount, string Currency, string ClientId)?>
            GetPaymentInfoAsync(Guid paymentId);
    }

    public interface IEmailSenderService
    {
        Task SendAsync(string to, string subject, string body, bool isHtml = true);
    }
}
