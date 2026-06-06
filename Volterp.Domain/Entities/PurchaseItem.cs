namespace Volterp.Domain.Entities;

public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }

    public Purchase Purchase { get; set; } = null!;
}