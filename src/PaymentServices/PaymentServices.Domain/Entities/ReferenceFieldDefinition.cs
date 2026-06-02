namespace PaymentServices.Domain.Entities;

public sealed class ReferenceFieldDefinition
{
	public Guid Id { get; set; }
	public Guid PayableServiceId { get; set; }
	public string FieldKey { get; set; } = string.Empty;
	public string DisplayLabel { get; set; } = string.Empty;
	public string DataType { get; set; } = string.Empty;
	public bool IsRequired { get; set; }
	public int? MinLength { get; set; }
	public int? MaxLength { get; set; }
	public string? ValidationRule { get; set; }
	public int DisplayOrder { get; set; }
}
