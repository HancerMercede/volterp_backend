using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;
using Volterp.ElectronicInvoicing;

namespace Volterp.Application.DTOs.ProductDtos;

public record CreateProductDto(
    string Name,
    string Category,
    string? Description,
    decimal Price,
    int Stock,
    Guid? CategoryId,
    string? ImageUrl = null,
    TaxCode TaxCode = TaxCode.ItbisTasa1,
    string? Code = null
):IMapTo<Product>
{
    public Product MapTo()
    {
        // CompanyId is NOT copied here — it is set by ProductService.CreateAsync
        // from the companyId parameter (which the controller reads from the JWT).
        // Code is copied as requested; ProductService.CreateAsync resolves it
        // (user code or generated sequence) and validates its uniqueness.
        return new Product
        {
           Name = Name,
           Category = Category,
           Description = Description,
            Price = new Money(Price, Currency.ARS),
           Stock = Stock,
            CategoryId = CategoryId,
            ImageUrl = ImageUrl,
            Code = Code?.Trim() ?? string.Empty
        };
    }
}