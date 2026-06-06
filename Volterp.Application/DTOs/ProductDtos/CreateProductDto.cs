using Volterp.Domain.Entities;

namespace Volterp.Application.DTOs.ProductDtos;

public record CreateProductDto(
    string Name,
    string Category,
    string? Description,
    decimal Price,
    int Stock,
    int? CategoryId,
    int CompanyId,
    string? ImageUrl = null
):IMapTo<Product>
{
    public Product MapTo()
    {
        return new Product
        {
           Name = Name,
           Category = Category,
           Description = Description,
           Price = Price,
           Stock = Stock,
           CategoryId = CategoryId,
           CompanyId = CompanyId,
        };
    }
}