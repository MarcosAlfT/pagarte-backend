using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Entities
{
	public class PaymentQuote
	{
		public Guid Id { get; set; }
		public string ClientId { get; set; } = string.Empty;
		public Guid ServiceId { get; set; }
		public string Currency { get; set; } = string.Empty;
		public decimal TotalAmount { get; set; }
		public PaymentQuoteStatus Status { get; set; } = PaymentQuoteStatus.Unpaid;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime ExpiresAt { get; set; }
		public DateTime? PaidAt { get; set; }

		public Service Service { get; set; } = null!;
		public ICollection<PaymentQuoteDetail> Details { get; set; } = [];
		public ICollection<Payment> Payments { get; set; } = [];

		public bool IsExpired(DateTime utcNow) => utcNow > ExpiresAt;

		public void MarkPaid()
		{
			Status = PaymentQuoteStatus.Paid;
			PaidAt = DateTime.UtcNow;
		}

		public static PaymentQuote Create(
			string clientId,
			Guid serviceId,
			string currency,
			decimal totalAmount,
			DateTime expiresAt)
		{
			return new PaymentQuote
			{
				Id = Guid.NewGuid(),
				ClientId = clientId,
				ServiceId = serviceId,
				Currency = currency,
				TotalAmount = totalAmount,
				ExpiresAt = expiresAt
			};
		}
	}
}
