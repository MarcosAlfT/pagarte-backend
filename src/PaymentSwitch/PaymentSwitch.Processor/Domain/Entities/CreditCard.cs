using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Entities
{
	public class CreditCard
	{
		public Guid Id { get; set; }
		public string ClientId { get; set; } = string.Empty;
		public string OperatorProvider { get; set; } = string.Empty;
		public string OperatorCardToken { get; set; } = string.Empty;
		public string CardNumber { get; set; } = string.Empty;
		public string CardHolderName { get; set; } = string.Empty;
		public string Last4Digits { get; set; } = string.Empty;
		public CardType CardType { get; set; }
		public int ExpiryMonth { get; set; }
		public int ExpiryYear { get; set; }
		public bool IsDefault { get; set; } = false;
		public DateTime CreatedAt { get; set; }
		public DateTime? UpdatedAt { get; set; }
		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedAt { get; set; }

		public ICollection<Payment> Payments { get; set; } = [];

		public static CreditCard Create(string clientId, string operatorProvider,
			string operatorCardToken,
			string cardNumber, string cardHolderName,
			string last4Digits, CardType cardType, int expiryMonth,
			int expiryYear, bool isDefault, DateTime utcNow)
		{
			return new CreditCard
			{
				Id = Guid.NewGuid(),
				ClientId = clientId,
				OperatorProvider = operatorProvider,
				OperatorCardToken = operatorCardToken,
				CardNumber = cardNumber,
				CardHolderName = cardHolderName,
				Last4Digits = last4Digits,
				CardType = cardType,
				ExpiryMonth = expiryMonth,
				ExpiryYear = expiryYear,
				IsDefault = isDefault,
				CreatedAt = utcNow
			};
		}

		public void Update(string cardHolderName, bool isDefault, DateTime utcNow)
		{
			CardHolderName = cardHolderName;
			IsDefault = isDefault;
			UpdatedAt = utcNow;
		}

		public void Delete(DateTime utcNow)
		{
			IsDeleted = true;
			DeletedAt = utcNow;
		}
	}
}
