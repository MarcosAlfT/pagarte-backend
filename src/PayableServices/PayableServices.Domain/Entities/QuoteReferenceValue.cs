namespace PayableServices.Domain.Entities;

public sealed class QuoteReferenceValue
{
	public Guid Id { get; set; }
	public Guid QuoteId { get; set; }
	public string Key { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
}
