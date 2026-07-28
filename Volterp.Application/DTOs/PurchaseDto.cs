using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs;

public record PurchaseDto : IMapFrom<Purchase>
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public EntityStatus Status { get; set; }
    public decimal Total { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public List<PurchaseItemDto> Items { get; set; } = new();

    public void MapFrom(Purchase source)
    {
        Id = source.Id;
        SupplierId = source.SupplierId;
        SupplierName = source.SupplierName;
        Status = source.Status;
        Total = source.Total;
        Notes = source.Notes;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
        CreatedBy = source.CreatedBy;
        UpdatedBy = source.UpdatedBy;
        Items = source.Items.Select(i =>
        {
            var dto = new PurchaseItemDto();
            dto.MapFrom(i);
            return dto;
        }).ToList();
    }
}
