using PaymentSwitch.Messaging;
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
		public PaymentTransactionStatus Status { get; set; } =
			PaymentTransactionStatus.Confirmed;
		public string Currency { get; set; } = string.Empty;
		public string Reference { get; set; } = string.Empty;
		public string? ErrorMessage { get; set; }
		public int RetryCount { get; set; } = 0;
		public DateTime? NextRetryAt { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? ProcessedAt { get; set; }
		public DateTime? LastUpdatedAt { get; set; }

		public CreditCard CreditCard { get; set; } = null!;
		public PaymentQuote? Quote { get; set; }
		public Service Service { get; set; } = null!;
		public ICollection<PaymentDetail> Details { get; set; } = [];

		public static Payment Create(string clientId, Guid quoteId, Guid creditCardId,
			Guid serviceId, string currency, string operatorProvider, DateTime utcNow)
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
				Reference = GenerateReference(utcNow),
				Status = PaymentTransactionStatus.Confirmed,
				CreatedAt = utcNow
			};
		}

		public void UpdateStatus(
			PaymentTransactionStatus status,
			DateTime utcNow,
			string? errorMessage = null)
		{
			Status = status;
			ErrorMessage = errorMessage;
			LastUpdatedAt = utcNow;

			if (status is PaymentTransactionStatus.Completed
				or PaymentTransactionStatus.CompanyPaymentFailed
				or PaymentTransactionStatus.Failed
				or PaymentTransactionStatus.Refunded
				or PaymentTransactionStatus.RefundFailed)
			{
				ProcessedAt = utcNow;
			}
		}

		public void SetOperatorPaymentId(string operatorPaymentId, DateTime utcNow)
		{
			OperatorPaymentId = operatorPaymentId;
			LastUpdatedAt = utcNow;
		}

		public void SetCompanyReference(string companyReference, DateTime utcNow)
		{
			CompanyReference = companyReference;
			LastUpdatedAt = utcNow;
		}

		public void IncrementRetry(DateTime utcNow)
		{
			RetryCount++;
			NextRetryAt = utcNow.AddMinutes(5);
			LastUpdatedAt = utcNow;
		}

		private static string GenerateReference(DateTime utcNow) =>
			$"PAG-{utcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
	}
}
