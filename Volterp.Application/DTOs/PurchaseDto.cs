using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs;

public record PurchaseDto : IMapFrom<Purchase>
{
    public Guid Id { get; set; }
    public Guid? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public EntityStatus Status { get; set; }
    public decimal Total { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
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
