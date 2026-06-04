namespace PaymentSwitch.Worker.Interfaces
{
    public interface IPaymentStatusRepository
    {
        Task UpdateStatusAsync(Guid paymentId, string status,
            string? companyReference = null, string? errorMessage = null);
        Task IncrementRetryAsync(Guid paymentId);
        Task<(string? OperatorPaymentId, decimal Amount, string Currency, string ClientId)?>
            GetPaymentInfoAsync(Guid paymentId);
    }

    public interface IEmailSenderService
    {
        Task SendAsync(string to, string subject, string body, bool isHtml = true);
    }
}
