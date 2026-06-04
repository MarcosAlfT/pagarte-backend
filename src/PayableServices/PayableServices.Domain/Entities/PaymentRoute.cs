namespace PayableServices.Domain.Entities;

public sealed class PaymentRoute
{
	public Guid Id { get; set; }
	public Guid PayableServiceId { get; set; }
	public string RouteType { get; set; } = string.Empty;
	public Guid? PaymentNetworkId { get; set; }
	public Guid? ProviderId { get; set; }
	public string? ExternalSourceCode { get; set; }
	public string? ExternalServiceCode { get; set; }
	public string Status { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public DateTime? LastTestedAt { get; set; }
}
