using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Entities
{
	public class Payment
	{
		public Guid Id { get; set; }
		public string ClientId { get; set; } = string.Empty;
		public Guid? QuoteId { get; set; }
		public Guid CreditCardId { get; set; }
		public Guid ServiceId { get; set; }
		public string OperatorProvider { get; set; } = string.Empty;
		public string? OperatorPaymentId { get; set; }
		public string? CompanyReference { get; set; }
		public TransactionStatus Status { get; set; } = TransactionStatus.Confirmed;
		public string Currency { get; set; } = string.Empty;
		public string Reference { get; set; } = string.Empty;
		public string? ErrorMessage { get; set; }
		public int RetryCount { get; set; } = 0;
		public DateTime? NextRetryAt { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? ProcessedAt { get; set; }
		public DateTime? LastUpdatedAt { get; set; }

		public CreditCard CreditCard { get; set; } = null!;
		public PaymentQuote? Quote { get; set; }
		public Service Service { get; set; } = null!;
		public ICollection<PaymentDetail> Details { get; set; } = [];

		public static Payment Create(string clientId, Guid quoteId, Guid creditCardId,
			Guid serviceId, string currency, string operatorProvider)
		{
			return new Payment
			{
				Id = Guid.NewGuid(),
				ClientId = clientId,
				QuoteId = quoteId,
				CreditCardId = creditCardId,
				ServiceId = serviceId,
				OperatorProvider = operatorProvider,
				Currency = currency,
				Reference = GenerateReference(),
				Status = TransactionStatus.Confirmed,
				CreatedAt = DateTime.UtcNow
			};
		}

		public void UpdateStatus(TransactionStatus status, string? errorMessage = null)
		{
			Status = status;
			ErrorMessage = errorMessage;
			LastUpdatedAt = DateTime.UtcNow;

			if (status is TransactionStatus.Completed
				or TransactionStatus.CompanyPaymentFailed
				or TransactionStatus.Failed
				or TransactionStatus.Refunded
				or TransactionStatus.RefundFailed)
			{
				ProcessedAt = DateTime.UtcNow;
			}
		}

		public void SetOperatorPaymentId(string operatorPaymentId)
		{
			OperatorPaymentId = operatorPaymentId;
			LastUpdatedAt = DateTime.UtcNow;
		}

		public void SetCompanyReference(string companyReference)
		{
			CompanyReference = companyReference;
			LastUpdatedAt = DateTime.UtcNow;
		}

		public void IncrementRetry()
		{
			RetryCount++;
			NextRetryAt = DateTime.UtcNow.AddMinutes(5);
			LastUpdatedAt = DateTime.UtcNow;
		}

		private static string GenerateReference() =>
			$"PAG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
	}
}
