namespace PayableServices.Domain.Entities;

public sealed class Subcategory
{
	public Guid Id { get; set; }
	public Guid CategoryId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int DisplayOrder { get; set; }
	public string SearchKeywords { get; set; } = string.Empty;
}
