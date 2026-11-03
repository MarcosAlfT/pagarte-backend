namespace PayableServices.Domain.Entities;

public sealed class ExternalCatalogueSource
{
    public long SnapshotVersion { get; set; }
    public string SnapshotFingerprint { get; set; } = "";
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public Guid CountryId { get; set; }
    public bool IsActive { get; set; }
}
