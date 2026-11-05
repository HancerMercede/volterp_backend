using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Volterp.ElectronicInvoicing;

namespace Volterp.Domain.Entities;

public class Sale : AuditEntity, ITenantScoped
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    
    public Guid? ClienteId { get; set; }
    public string? ClienteName { get; set; }
    
    public SaleStatus Status { get; set; } = SaleStatus.Pending;
    
    public EcfDocumentType DocumentType { get; set; } = EcfDocumentType.Consumer;
    public string? ReceptorTaxId { get; set; }
    
    public decimal Subtotal { get; set; }
    public decimal Itbis { get; set; }
    public decimal Total { get; set; }
    
    public string? Notes { get; set; }
    
    public Company Company { get; set; } = null!;
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<ElectronicInvoice> ElectronicInvoices { get; set; } = new List<ElectronicInvoice>();

    public void Complete()
    {
        if (Status != SaleStatus.Pending)
            throw new InvalidSaleStateTransitionException(Status.ToString(), SaleStatus.Completed.ToString());

        if (Items.Count == 0)
            throw new InvalidSaleStateTransitionException(
                Status.ToString(), SaleStatus.Completed.ToString(),
                "Sale must have at least one item before completion.");

        if (DocumentType == EcfDocumentType.FiscalCredit && string.IsNullOrWhiteSpace(ReceptorTaxId))
            throw new InvalidSaleStateTransitionException(
                Status.ToString(), SaleStatus.Completed.ToString(),
                "Sale must have the receptor tax id to be completed as a fiscal credit.");

        Status = SaleStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != SaleStatus.Pending)
            throw new InvalidSaleStateTransitionException(Status.ToString(), SaleStatus.Cancelled.ToString());

        Status = SaleStatus.Cancelled;
    }

    public void AddItem(Guid productId, string? productName, int quantity, decimal unitPrice, TaxCode taxCode)
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
            Subtotal = subtotal,
            TaxCode = taxCode
        });

        CalculateTotals();
    }

    public void RemoveItem(Guid productId)
    {
        var item = Items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
            return;

        Items.Remove(item);

        CalculateTotals();
    }

    public void CalculateTotals()
    {
        var subtotal = 0m;
        var itbis = 0m;

        foreach (var item in Items)
        {
            subtotal += item.Subtotal;
            itbis += decimal.Round(item.Subtotal * TaxRate(item.TaxCode), 2);
        }

        Subtotal = decimal.Round(subtotal, 2);
        Itbis = decimal.Round(itbis, 2);
        Total = Subtotal + Itbis;
    }

    private static decimal TaxRate(TaxCode taxCode) => EcfTaxRates.FractionOf(taxCode);
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
    public TaxCode TaxCode { get; set; } = TaxCode.ItbisTasa1;

    public Sale Sale { get; set; } = null!;
}
