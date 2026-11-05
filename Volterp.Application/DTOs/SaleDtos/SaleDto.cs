using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.SaleDtos;

public record SaleDto:IMapFrom<Sale>
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? ClienteId { get; set; }
    public string? ClienteName { get; set; }
    public SaleStatus Status { get; set; }
    public EcfDocumentType DocumentType { get; set; }
    public string? ReceptorTaxId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Itbis { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<SaleItemDto>? Items { get; set; }

    public void MapFrom(Sale source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        Code = source.Code;
        ClienteId = source.ClienteId;
        ClienteName = source.ClienteName;
        Status = source.Status;
        DocumentType = source.DocumentType;
        ReceptorTaxId = source.ReceptorTaxId;
        Subtotal = source.Subtotal;
        Itbis = source.Itbis;
        Total = source.Total;
        Notes = source.Notes;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
        Items = MapToSaleItemDto(source.Items.ToList());
    }

    private static List<SaleItemDto> MapToSaleItemDto(List<SaleItem> sources)
    {   
        return sources.Map(x =>
            new SaleItemDto(
                x.Id,
                x.ProductId,
                x.ProductName,
                x.ProductCategory,
                x.ProductCode,
                x.ProductImageUrl,
                x.Quantity,
                x.UnitPrice,
                x.Subtotal,
                x.TaxCode));
    }
}
