using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.CategoryDtos;

public record CategoryDto:IMapFrom<Category>
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public Guid CompanyId  { get; set; }
    public bool IsActive  { get; set; }
    public DateTime CreatedAt  { get; set; }

    public void MapFrom(Category source)
    {
        Id = source.Id;
        Code = source.Code;
        Name = source.Name;
        Description = source.Description;
        CompanyId = source.CompanyId;
        IsActive = source.IsActive;
        CreatedAt = source.CreatedAt;
    }
}
