namespace PaymentSwitch.Processor.Domain.Entities
{
	public class Service
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string Category { get; set; } = string.Empty;
		public Guid CompanyId { get; set; }
		public decimal BaseAmount { get; set; }
		public string Currency { get; set; } = string.Empty;
		public bool IsActive { get; set; } = true;
		public Company Company { get; set; } = null!;
		public ICollection<Payment> Payments { get; set; } = [];
	}
}
