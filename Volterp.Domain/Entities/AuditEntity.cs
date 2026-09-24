namespace Volterp.Domain.Entities;

public abstract class AuditEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCreatedBy(Guid createdBy)
    {
        CreatedBy = createdBy;
    }

    public void SetUpdatedBy(Guid updatedBy)
    {
        UpdatedBy = updatedBy;
    }
}
