using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Entities
{
	public class PaymentQuoteDetail
	{
		public Guid Id { get; set; }
		public Guid PaymentQuoteId { get; set; }
		public PaymentDetailType Type { get; set; }
		public string Description { get; set; } = string.Empty;
		public decimal Amount { get; set; }
		public string Currency { get; set; } = string.Empty;

		public PaymentQuote PaymentQuote { get; set; } = null!;

		public static PaymentQuoteDetail Create(Guid paymentQuoteId, PaymentDetailType type,
			string description, decimal amount, string currency)
		{
			return new PaymentQuoteDetail
			{
				Id = Guid.NewGuid(),
				PaymentQuoteId = paymentQuoteId,
				Type = type,
				Description = description,
				Amount = amount,
				Currency = currency
			};
		}
	}
}
