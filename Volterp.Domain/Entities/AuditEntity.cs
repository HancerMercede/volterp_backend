namespace Volterp.Domain.Entities;

public abstract class AuditEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCreatedBy(int createdBy)
    {
        CreatedBy = createdBy;
    }
}
