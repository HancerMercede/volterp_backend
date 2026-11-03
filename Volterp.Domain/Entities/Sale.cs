using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;

namespace Volterp.Domain.Entities;

public class Sale : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    
    public Guid? ClienteId { get; set; }
    public string? ClienteName { get; set; }
    
    public SaleStatus Status { get; set; } = SaleStatus.Pending;
    
    public decimal Total { get; set; }
    
    public string? Notes { get; set; }
    
    public Company Company { get; set; } = null!;
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

    public void Complete()
    {
        if (Status != SaleStatus.Pending)
            throw new InvalidSaleStateTransitionException(Status.ToString(), SaleStatus.Completed.ToString());

        if (Items.Count == 0)
            throw new InvalidSaleStateTransitionException(
                Status.ToString(), SaleStatus.Completed.ToString(),
                "Sale must have at least one item before completion.");

        Status = SaleStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != SaleStatus.Pending)
            throw new InvalidSaleStateTransitionException(Status.ToString(), SaleStatus.Cancelled.ToString());

        Status = SaleStatus.Cancelled;
    }

    public void AddItem(Guid productId, string? productName, int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        var subtotal = quantity * unitPrice;

        Items.Add(new SaleItem
        {
            ProductId = productId,
            ProductName = productName ?? string.Empty,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = subtotal
        });

        Total += subtotal;
    }

    public void RemoveItem(Guid productId)
    {
        var item = Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
            return;

        Total -= item.Subtotal;
        Items.Remove(item);
    }
}


public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ProductCategory { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductImageUrl { get; set; }
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public Sale Sale { get; set; } = null!;
}
