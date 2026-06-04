using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Entities
{
	public class PaymentDetail
	{
		public Guid Id { get; set; }
		public Guid PaymentId { get; set; }
		public PaymentDetailType Type { get; set; }
		public string Description { get; set; } = string.Empty;
		public decimal Amount { get; set; }
		public string Currency { get; set; } = string.Empty;
		public Payment Payment { get; set; } = null!;

		public static PaymentDetail Create(Guid paymentId, PaymentDetailType type,
			string description, decimal amount, string currency)
		{
			return new PaymentDetail
			{
				Id = Guid.NewGuid(),
				PaymentId = paymentId,
				Type = type,
				Description = description,
				Amount = amount,
				Currency = currency
			};
		}
	}
}
