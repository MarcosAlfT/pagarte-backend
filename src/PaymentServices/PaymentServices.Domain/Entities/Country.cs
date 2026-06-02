namespace PaymentServices.Domain.Entities;

public sealed class Country
{
	public Guid Id { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Currency { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int DisplayOrder { get; set; }
}
