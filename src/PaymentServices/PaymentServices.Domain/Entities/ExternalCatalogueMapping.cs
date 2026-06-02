using PaymentServices.Domain.Enums;

namespace PaymentServices.Domain.Entities;

public sealed class ExternalCatalogueMapping
{
	public Guid Id { get; set; }
	public Guid ExternalCatalogueItemId { get; set; }
	public Guid PayableServiceId { get; set; }
	public Guid PaymentRouteId { get; set; }
	public ExternalCatalogueMappingStatus MappingStatus { get; set; }
	public string? ReviewReason { get; set; }
}
