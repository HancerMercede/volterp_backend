using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;

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
    public Company Company { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();

    public void Complete()
    {
        if (Status != EntityStatus.Pending)
            throw new InvalidSaleStateTransitionException(Status.ToString(), EntityStatus.Completed.ToString());

        if (Items.Count == 0)
            throw new InvalidSaleStateTransitionException(
                Status.ToString(), EntityStatus.Completed.ToString(),
                "Purchase must have at least one item before completion.");

        Status = EntityStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != EntityStatus.Pending)
            throw new InvalidSaleStateTransitionException(Status.ToString(), EntityStatus.Cancelled.ToString());

        Status = EntityStatus.Cancelled;
    }

    public void AddItem(int? productId, string productName, int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        var subtotal = quantity * unitPrice;

        Items.Add(new PurchaseItem
        {
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Subtotal = subtotal
        });

        Total += subtotal;
    }

    public void RemoveItem(int? productId)
    {
        var item = Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
            return;

        Total -= item.Subtotal;
        Items.Remove(item);
    }
}
