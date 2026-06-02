namespace PaymentServices.Domain.Common;

public abstract class AuditableEntity
{
	public Guid Id { get; set; }
	public DateTime CreatedAt { get; set; }
	public string? CreatedBy { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public string? UpdatedBy { get; set; }
	public DateTime? DeletedAt { get; set; }
	public string? DeletedBy { get; set; }
}
