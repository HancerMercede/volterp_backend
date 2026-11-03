using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Volterp.Domain.ValueObjects;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Domain;

public class ProductBehaviorTests
{
    // ===== DeductStock =====

    [Fact]
    public void DeductStock_WithSufficientStock_ReducesStock()
    {
        var product = CreateProduct(stock: 10);

        product.DeductStock(5);

        product.Stock.Should().Be(5);
    }

    [Fact]
    public void DeductStock_WithExactStock_ReducesToZero()
    {
        var product = CreateProduct(stock: 10);

        product.DeductStock(10);

        product.Stock.Should().Be(0);
    }

    [Fact]
    public void DeductStock_WithInsufficientStock_ThrowsInsufficientStockException()
    {
        var product = CreateProduct(stock: 10);

        var act = () => product.DeductStock(15);

        act.Should().Throw<InsufficientStockException>()
            .Which.Requested.Should().Be(15);
        product.Stock.Should().Be(10);
    }

    [Fact]
    public void DeductStock_WithInsufficientStock_ExceptionContainsRequestedAndAvailable()
    {
        var product = CreateProduct(stock: 10);

        var act = () => product.DeductStock(15);

        var ex = Record.Exception(act) as InsufficientStockException;
        ex.Should().NotBeNull();
        ex!.Requested.Should().Be(15);
        ex.Available.Should().Be(10);
    }

    [Fact]
    public void DeductStock_WithZeroQuantity_ThrowsArgumentException()
    {
        var product = CreateProduct(stock: 10);

        var act = () => product.DeductStock(0);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    [Fact]
    public void DeductStock_WithNegativeQuantity_ThrowsArgumentException()
    {
        var product = CreateProduct(stock: 10);

        var act = () => product.DeductStock(-1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    [Fact]
    public void DeductStock_WithZeroStock_ThrowsInsufficientStockException()
    {
        var product = CreateProduct(stock: 0);

        var act = () => product.DeductStock(1);

        act.Should().Throw<InsufficientStockException>()
            .Which.Available.Should().Be(0);
    }

    [Fact]
    public void DeductStock_AllowNegativeTrue_BypassesStockCheck()
    {
        var product = CreateProduct(stock: 10);

        product.DeductStock(15, allowNegative: true);

        product.Stock.Should().Be(-5);
    }

    // ===== IncrementStock =====

    [Fact]
    public void IncrementStock_IncreasesStock()
    {
        var product = CreateProduct(stock: 10);

        product.IncrementStock(5);

        product.Stock.Should().Be(15);
    }

    [Fact]
    public void IncrementStock_WithZeroQuantity_ThrowsArgumentException()
    {
        var product = CreateProduct(stock: 10);

        var act = () => product.IncrementStock(0);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    [Fact]
    public void IncrementStock_WithNegativeQuantity_ThrowsArgumentException()
    {
        var product = CreateProduct(stock: 10);

        var act = () => product.IncrementStock(-1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    // ===== Helpers =====

    private static Product CreateProduct(int stock = 10)
    {
        return new Product
        {
            Id = Guid7TestValues.ProductA,
            Name = "Test Product",
            Price = new Money(100, Currency.ARS),
            Stock = stock,
            IsActive = true
        };
    }
}
