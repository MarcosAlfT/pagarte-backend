using PaymentSwitch.Processor.Domain.Enums;

namespace PaymentSwitch.Processor.Domain.Entities
{
	public class FeeConfiguration
	{
		public Guid Id { get; set; }
		public FeeType Type { get; set; }
		public CalculationType CalculationType { get; set; }
		public decimal Value { get; set; }
		public string Currency { get; set; } = string.Empty;
		public bool IsActive { get; set; } = true;
		public DateTime EffectiveDate { get; set; }
		public DateTime? EndDate { get; set; }
	}
}
