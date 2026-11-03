using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Volterp.Domain.ValueObjects;

namespace Volterp.Domain.Entities;

public class Product : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Stock { get; set; }
    public Money Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? CategoryId { get; set; }

    public Company Company { get; set; } = null!;
    public Category? CategoryEntity { get; set; }

    public void DeductStock(int quantity, bool allowNegative = false)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        if (!allowNegative && quantity > Stock)
            throw new InsufficientStockException(quantity, Stock);

        Stock -= quantity;
    }

    public void IncrementStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        Stock += quantity;
    }

    public void SetPrice(Money newPrice)
    {
        Price = newPrice;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
