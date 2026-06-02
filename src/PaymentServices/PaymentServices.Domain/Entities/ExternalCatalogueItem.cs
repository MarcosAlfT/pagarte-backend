namespace PaymentServices.Domain.Entities;

public sealed class ExternalCatalogueItem
{
	public Guid Id { get; set; }
	public Guid ExternalCatalogueSourceId { get; set; }
	public string ExternalCategory { get; set; } = string.Empty;
	public string ExternalSubcategory { get; set; } = string.Empty;
	public string ExternalName { get; set; } = string.Empty;
	public string ExternalCode { get; set; } = string.Empty;
	public string ExternalStatus { get; set; } = string.Empty;
	public bool IsAvailable { get; set; }
	public DateTime LastSeenAt { get; set; }
	public string? RawReference { get; set; }
}
