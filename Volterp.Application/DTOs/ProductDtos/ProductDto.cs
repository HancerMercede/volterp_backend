using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.ProductDtos;

public record ProductDto : IMapFrom<Product>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int? CategoryId { get; set; }
    public int CompanyId { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public void MapFrom(Product source)
    {
        Id = source.Id;
        Name = source.Name;
        Category = source.Category;
        Description = source.Description;
        Price = source.Price;
        Stock = source.Stock;
        CategoryId = source.CategoryId;
        CompanyId = source.CompanyId;
        IsActive = source.IsActive;
        ImageUrl = source.ImageUrl;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
    }
}


