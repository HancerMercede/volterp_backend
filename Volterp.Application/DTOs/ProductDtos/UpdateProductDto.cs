using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.ProductDtos;

public record UpdateProductDto(
    string Name,
    string Category,
    string? Description,
    decimal Price,
    int Stock,
    Guid? CategoryId,
    bool IsActive,
    string? ImageUrl = null,
    TaxCode TaxCode = TaxCode.ItbisTasa1
):IMapTo<Product>
{
    public Product MapTo()
    {
        return new Product
        {
            Name = Name,
            Category = Category,
            Description = Description,
            Price = new Money(Price, Currency.ARS),
            Stock = Stock,
            CategoryId = CategoryId,
            ImageUrl =  ImageUrl
        };
    }
}