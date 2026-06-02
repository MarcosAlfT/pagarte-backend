namespace PaymentServices.Domain.Entities;

public sealed class ExternalCatalogueSource
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string SourceType { get; set; } = string.Empty;
	public Guid CountryId { get; set; }
	public bool IsActive { get; set; }
}
