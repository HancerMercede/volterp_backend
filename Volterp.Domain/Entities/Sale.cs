using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

public class Sale : AuditEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    
    public int? ClienteId { get; set; }
    public string? ClienteName { get; set; }
    
    public SaleStatus Status { get; set; } = SaleStatus.Pending;
    
    public decimal Total { get; set; }
    
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}


public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCategory { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductImageUrl { get; set; }
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public Sale Sale { get; set; } = null!;
}