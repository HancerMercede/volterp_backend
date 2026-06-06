using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class Purchase : AuditEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public EntityStatus Status { get; set; } = EntityStatus.Pending;
    public decimal Total { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }

    public Company Company { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}