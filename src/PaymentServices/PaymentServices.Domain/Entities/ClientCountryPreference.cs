namespace PaymentServices.Domain.Entities;

public sealed class ClientCountryPreference
{
	public Guid Id { get; set; }
	public string ClientId { get; set; } = string.Empty;
	public Guid CountryId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
