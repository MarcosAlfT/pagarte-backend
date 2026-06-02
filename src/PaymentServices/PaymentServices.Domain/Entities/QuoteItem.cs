namespace PaymentServices.Domain.Entities;

public sealed class QuoteItem
{
	public Guid Id { get; set; }
	public Guid QuoteId { get; set; }
	public Guid PayableServiceId { get; set; }
	public string Description { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string Currency { get; set; } = string.Empty;
}
