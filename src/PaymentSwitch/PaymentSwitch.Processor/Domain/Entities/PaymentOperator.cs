using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Entities
{
	public class PaymentOperator
	{
		public Guid Id { get; set; }
		public string Code { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public PaymentOperatorScope Scope { get; set; } = PaymentOperatorScope.International;
		public int Priority { get; set; } = 100;
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; set; }

		public static PaymentOperator Create(
			string code,
			string name,
			PaymentOperatorScope scope,
			int priority = 100)
		{
			return new PaymentOperator
			{
				Id = Guid.NewGuid(),
				Code = code,
				Name = name,
				Scope = scope,
				Priority = priority,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};
		}
	}
}
