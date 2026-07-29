using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Xunit;

namespace Volterp.Tests.Domain;

public class SaleBehaviorTests
{
    // ===== Complete =====

    [Fact]
    public void Complete_WhenPending_TransitionsToCompleted()
    {
        var sale = CreateSale(items: [CreateSaleItem()]);

        sale.Complete();

        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public void Complete_WhenCompleted_ThrowsInvalidSaleStateTransitionException()
    {
        var sale = CreateSale(items: [CreateSaleItem()], status: SaleStatus.Completed);

        var act = () => sale.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .Which.FromStatus.Should().Be("Completed");
    }

    [Fact]
    public void Complete_WhenCancelled_ThrowsInvalidSaleStateTransitionException()
    {
        var sale = CreateSale(items: [CreateSaleItem()], status: SaleStatus.Cancelled);

        var act = () => sale.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .Which.FromStatus.Should().Be("Cancelled");
    }

    [Fact]
    public void Complete_WithNoItems_ThrowsInvalidSaleStateTransitionException()
    {
        var sale = CreateSale(items: []);

        var act = () => sale.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .WithMessage("*item*");
    }

    // ===== Cancel =====

    [Fact]
    public void Cancel_WhenPending_TransitionsToCancelled()
    {
        var sale = CreateSale(items: [CreateSaleItem()]);

        sale.Cancel();

        sale.Status.Should().Be(SaleStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsInvalidSaleStateTransitionException()
    {
        var sale = CreateSale(items: [CreateSaleItem()], status: SaleStatus.Completed);

        var act = () => sale.Cancel();

        act.Should().Throw<InvalidSaleStateTransitionException>();
    }

    [Fact]
    public void Cancel_WhenCancelled_ThrowsInvalidSaleStateTransitionException()
    {
        var sale = CreateSale(items: [CreateSaleItem()], status: SaleStatus.Cancelled);

        var act = () => sale.Cancel();

        act.Should().Throw<InvalidSaleStateTransitionException>();
    }

    // ===== AddItem =====

    [Fact]
    public void AddItem_ToEmptyItems_AddsOneItem()
    {
        var sale = CreateSale(items: []);

        sale.AddItem(productId: 1, productName: "Product A", quantity: 2, unitPrice: 100);

        sale.Items.Should().HaveCount(1);
        sale.Items.First().ProductId.Should().Be(1);
        sale.Items.First().Quantity.Should().Be(2);
        sale.Items.First().UnitPrice.Should().Be(100);
        sale.Items.First().Subtotal.Should().Be(200);
    }

    [Fact]
    public void AddItem_CalculatesCorrectSubtotal()
    {
        var sale = CreateSale(items: []);

        sale.AddItem(productId: 1, productName: "Product A", quantity: 3, unitPrice: 50);

        sale.Items.First().Subtotal.Should().Be(150);
    }

    [Fact]
    public void AddItem_WithSecondItem_IncreasesItemCount()
    {
        var sale = CreateSale(items:
        [
            new SaleItem { ProductId = 1, ProductName = "A", Quantity = 1, UnitPrice = 10, Subtotal = 10 }
        ]);

        sale.AddItem(productId: 2, productName: "Product B", quantity: 3, unitPrice: 50);

        sale.Items.Should().HaveCount(2);
    }

    [Fact]
    public void AddItem_WithNullProductName_IsAllowed()
    {
        var sale = CreateSale(items: []);

        var act = () => sale.AddItem(productId: 1, productName: null, quantity: 1, unitPrice: 100);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddItem_WithZeroQuantity_ThrowsArgumentException()
    {
        var sale = CreateSale(items: []);

        var act = () => sale.AddItem(productId: 1, productName: "P", quantity: 0, unitPrice: 100);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    [Fact]
    public void AddItem_WithNegativeUnitPrice_ThrowsArgumentException()
    {
        var sale = CreateSale(items: []);

        var act = () => sale.AddItem(productId: 1, productName: "P", quantity: 1, unitPrice: -50);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*price*");
    }

    // ===== RemoveItem =====

    [Fact]
    public void RemoveItem_ExistingItem_RemovesIt()
    {
        var sale = CreateSale(items:
        [
            new SaleItem { ProductId = 1, ProductName = "A", Quantity = 2, UnitPrice = 100, Subtotal = 200 }
        ]);

        sale.RemoveItem(1);

        sale.Items.Should().HaveCount(0);
    }

    [Fact]
    public void RemoveItem_NonExistentProductId_IsNoOp()
    {
        var sale = CreateSale(items:
        [
            new SaleItem { ProductId = 1, ProductName = "A", Quantity = 1, UnitPrice = 10, Subtotal = 10 }
        ]);

        var act = () => sale.RemoveItem(99);

        act.Should().NotThrow();
        sale.Items.Should().HaveCount(1);
    }

    // ===== Helpers =====

    private static Sale CreateSale(List<SaleItem>? items = null, SaleStatus status = SaleStatus.Pending)
    {
        return new Sale
        {
            Id = 1,
            Status = status,
            Items = items ?? [CreateSaleItem()]
        };
    }

    private static SaleItem CreateSaleItem()
    {
        return new SaleItem
        {
            ProductId = 1,
            ProductName = "Test Product",
            Quantity = 1,
            UnitPrice = 100,
            Subtotal = 100
        };
    }
}
