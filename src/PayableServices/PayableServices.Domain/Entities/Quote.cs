using PayableServices.Domain.Enums;

namespace PayableServices.Domain.Entities;

public sealed class Quote
{
	public Guid Id { get; set; }
	public string ClientId { get; set; } = string.Empty;
	public Guid ServiceId { get; set; }
	public Guid CountryId { get; set; }
	public string Currency { get; set; } = string.Empty;
	public QuoteStatus Status { get; set; } = QuoteStatus.Unpaid;
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public decimal TotalAmount { get; set; }
	public string ServiceName { get; set; } = string.Empty;
	public ICollection<QuoteItem> Items { get; set; } = [];
}
