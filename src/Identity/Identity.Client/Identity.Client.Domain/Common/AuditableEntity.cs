namespace Identity.Client.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public string? UpdatedBy { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }
    public string? DeletedBy { get; protected set; }

    public bool IsDeleted => DeletedAt is not null;

    protected void CreateAudit(DateTime now, string actor)
    {
        CreatedAt = now;
        CreatedBy = actor;
    }

    protected void UpdateAudit(DateTime now, string actor)
    {
        UpdatedAt = now;
        UpdatedBy = actor;
    }

    public virtual void SoftDelete(DateTime now, string actor)
    {
        if (DeletedAt is not null)
        {
            return;
        }

        DeletedAt = now;
        DeletedBy = actor;
        UpdateAudit(now, actor);
    }
}
