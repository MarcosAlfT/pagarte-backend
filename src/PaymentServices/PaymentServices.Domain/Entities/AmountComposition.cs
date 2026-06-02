namespace PaymentServices.Domain.Entities;

public sealed class AmountComposition
{
	public Guid Id { get; set; }
	public Guid PayableServiceId { get; set; }
	public ICollection<AmountComponent> Components { get; set; } = [];
}
