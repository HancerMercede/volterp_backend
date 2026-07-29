using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Xunit;

namespace Volterp.Tests.Domain;

public class PurchaseBehaviorTests
{
    // ===== Complete =====

    [Fact]
    public void Complete_WhenPending_TransitionsToCompleted()
    {
        var purchase = CreatePurchase(items: [CreatePurchaseItem()]);

        purchase.Complete();

        purchase.Status.Should().Be(EntityStatus.Completed);
    }

    [Fact]
    public void Complete_WhenCompleted_ThrowsInvalidSaleStateTransitionException()
    {
        var purchase = CreatePurchase(items: [CreatePurchaseItem()], status: EntityStatus.Completed);

        var act = () => purchase.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .Which.FromStatus.Should().Be("Completed");
    }

    [Fact]
    public void Complete_WhenCancelled_ThrowsInvalidSaleStateTransitionException()
    {
        var purchase = CreatePurchase(items: [CreatePurchaseItem()], status: EntityStatus.Cancelled);

        var act = () => purchase.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .Which.FromStatus.Should().Be("Cancelled");
    }

    [Fact]
    public void Complete_WithNoItems_ThrowsInvalidSaleStateTransitionException()
    {
        var purchase = CreatePurchase(items: []);

        var act = () => purchase.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .WithMessage("*item*");
    }

    // ===== Cancel =====

    [Fact]
    public void Cancel_WhenPending_TransitionsToCancelled()
    {
        var purchase = CreatePurchase(items: [CreatePurchaseItem()]);

        purchase.Cancel();

        purchase.Status.Should().Be(EntityStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsInvalidSaleStateTransitionException()
    {
        var purchase = CreatePurchase(items: [CreatePurchaseItem()], status: EntityStatus.Completed);

        var act = () => purchase.Cancel();

        act.Should().Throw<InvalidSaleStateTransitionException>();
    }

    [Fact]
    public void Cancel_WhenCancelled_ThrowsInvalidSaleStateTransitionException()
    {
        var purchase = CreatePurchase(items: [CreatePurchaseItem()], status: EntityStatus.Cancelled);

        var act = () => purchase.Cancel();

        act.Should().Throw<InvalidSaleStateTransitionException>();
    }

    // ===== AddItem =====

    [Fact]
    public void AddItem_ToEmptyItems_AddsOneItem()
    {
        var purchase = CreatePurchase(items: []);

        purchase.AddItem(productId: 1, productName: "Product A", quantity: 5, unitPrice: 200);

        purchase.Items.Should().HaveCount(1);
        purchase.Items.First().ProductId.Should().Be(1);
        purchase.Items.First().Quantity.Should().Be(5);
        purchase.Items.First().UnitPrice.Should().Be(200);
        purchase.Items.First().Subtotal.Should().Be(1000);
    }

    [Fact]
    public void AddItem_CalculatesCorrectSubtotal()
    {
        var purchase = CreatePurchase(items: []);

        purchase.AddItem(productId: 1, productName: "P", quantity: 3, unitPrice: 150);

        purchase.Items.First().Subtotal.Should().Be(450);
    }

    [Fact]
    public void AddItem_UpdatesTotal()
    {
        var purchase = CreatePurchase(items: []);

        purchase.AddItem(productId: 1, productName: "P", quantity: 2, unitPrice: 100);

        purchase.Total.Should().Be(200);
    }

    [Fact]
    public void AddItem_WithZeroQuantity_ThrowsArgumentException()
    {
        var purchase = CreatePurchase(items: []);

        var act = () => purchase.AddItem(productId: 1, productName: "P", quantity: 0, unitPrice: 100);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    [Fact]
    public void AddItem_WithNegativeQuantity_ThrowsArgumentException()
    {
        var purchase = CreatePurchase(items: []);

        var act = () => purchase.AddItem(productId: 1, productName: "P", quantity: -1, unitPrice: 100);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    // ===== RemoveItem =====

    [Fact]
    public void RemoveItem_ExistingItem_RemovesIt()
    {
        var purchase = CreatePurchase(items:
        [
            new PurchaseItem { ProductId = 1, ProductName = "A", Quantity = 2, UnitPrice = 100, Subtotal = 200 }
        ]);

        purchase.RemoveItem(1);

        purchase.Items.Should().HaveCount(0);
    }

    [Fact]
    public void RemoveItem_ExistingItem_AdjustsTotal()
    {
        var purchase = CreatePurchase(items:
        [
            new PurchaseItem { ProductId = 1, ProductName = "A", Quantity = 2, UnitPrice = 100, Subtotal = 200 },
            new PurchaseItem { ProductId = 2, ProductName = "B", Quantity = 1, UnitPrice = 50, Subtotal = 50 }
        ]);
        purchase.Total = 250;

        purchase.RemoveItem(1);

        purchase.Total.Should().Be(50);
    }

    [Fact]
    public void RemoveItem_NonExistentProductId_IsNoOp()
    {
        var purchase = CreatePurchase(items:
        [
            new PurchaseItem { ProductId = 1, ProductName = "A", Quantity = 1, UnitPrice = 10, Subtotal = 10 }
        ]);

        var act = () => purchase.RemoveItem(99);

        act.Should().NotThrow();
        purchase.Items.Should().HaveCount(1);
    }

    // ===== Helpers =====

    private static Purchase CreatePurchase(List<PurchaseItem>? items = null, EntityStatus status = EntityStatus.Pending)
    {
        return new Purchase
        {
            Id = 1,
            Status = status,
            Items = items ?? [CreatePurchaseItem()]
        };
    }

    private static PurchaseItem CreatePurchaseItem()
    {
        return new PurchaseItem
        {
            ProductId = 1,
            ProductName = "Test Product",
            Quantity = 1,
            UnitPrice = 100,
            Subtotal = 100
        };
    }
}
