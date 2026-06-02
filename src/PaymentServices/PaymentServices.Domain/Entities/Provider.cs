namespace PaymentServices.Domain.Entities;

public sealed class Provider
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid CountryId { get; set; }
	public bool IsActive { get; set; }
}
