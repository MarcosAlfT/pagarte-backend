namespace PaymentSwitch.Messaging.Messages
{
    /// <summary>
    /// Published by Worker after payment operator charge succeeds.
    /// Engine calls company API when it receives this.
    /// </summary>
    public class PaymentRequestMessage
    {
        public Guid PaymentId { get; set; }
        public Guid CompanyId { get; set; }
        public string OperatorProvider { get; set; } = string.Empty;
        public string OperatorPaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Published by Engine when company API fails.
    /// Engine processes refund with retry logic.
    /// </summary>
    public class RefundRequestMessage
    {
        public Guid PaymentId { get; set; }
        public string OperatorProvider { get; set; } = string.Empty;
        public string OperatorPaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int RetryCount { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Published by Worker (card registration) or Engine (payment results).
    /// Engine sends via SMTP.
    /// </summary>
    public class EmailMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsHtml { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Published by Engine when refund fails after max retries.
    /// For admin/support Ã¢â‚¬â€ manual intervention required.
    /// </summary>
    public class AlertMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? PaymentId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "Critical";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
