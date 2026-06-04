using PayableServices.Domain.Enums;

namespace PayableServices.Domain.Entities;

public sealed class PayableService
{
	public Guid Id { get; set; }
	public Guid CountryId { get; set; }
	public Guid CategoryId { get; set; }
	public Guid SubcategoryId { get; set; }
	public Guid ProviderId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int DisplayOrder { get; set; }
	public string SearchKeywords { get; set; } = string.Empty;
	public string Currency { get; set; } = string.Empty;
	public decimal BaseAmount { get; set; }
	public bool AllowsQuote { get; set; }
	public bool AllowsPayment { get; set; }
	public PayableServiceSelectionMode SelectionMode { get; set; } = PayableServiceSelectionMode.SingleItemOnly;
}
