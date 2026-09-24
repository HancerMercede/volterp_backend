using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.ValueObjects;

namespace Volterp.Application.DTOs.ProductDtos;

/// <summary>
/// Cycle 1.1 wire refactor — request-only DTO for products. Deliberately
/// excludes <c>CompanyId</c>: tenant scope is decided by the JWT-derived
/// <c>companyId</c> the controller passes to <c>ProductService.CreateAsync</c>.
/// Wire-contract guard lives in <c>WireContractTests</c>.
/// </summary>
public record CreateProductDto(
    string Name,
    string Category,
    string? Description,
    decimal Price,
    int Stock,
    Guid? CategoryId,
    string? ImageUrl = null
):IMapTo<Product>
{
    public Product MapTo()
    {
        // CompanyId is NOT copied here — it is set by ProductService.CreateAsync
        // from the companyId parameter (which the controller reads from the JWT).
        return new Product
        {
           Name = Name,
           Category = Category,
           Description = Description,
            Price = new Money(Price, Currency.ARS),
           Stock = Stock,
           CategoryId = CategoryId,
           ImageUrl = ImageUrl
        };
    }
}