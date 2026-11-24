using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Xunit;
using Volterp.Tests.TestHelpers;
using Volterp.ElectronicInvoicing;

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

    [Fact]
    public void Complete_WhenFiscalCreditWithoutReceptorTaxId_ThrowsInvalidSaleStateTransitionException()
    {
        var sale = CreateSale(items: [CreateSaleItem()], documentType: EcfDocumentType.FiscalCredit);

        var act = () => sale.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .WithMessage("*receptor tax id*");
    }

    [Fact]
    public void Complete_WhenFiscalCreditWithReceptorTaxId_TransitionsToCompleted()
    {
        var sale = CreateSale(items: [CreateSaleItem()], documentType: EcfDocumentType.FiscalCredit);
        sale.ReceptorTaxId = "101010101";

        sale.Complete();

        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public void Complete_WhenConsumptionOf250000OrMoreWithoutReceptorTaxId_ThrowsInvalidSaleStateTransitionException()
    {
        // From RD$250,000 the consumption invoice is nominative: it names
        // its buyer. Below that the buyer is the final consumer and the
        // document carries no buyer at all.
        var sale = CreateSale(items: [CreateSaleItem()], documentType: EcfDocumentType.Consumer, total: 250000m);

        var act = () => sale.Complete();

        act.Should().Throw<InvalidSaleStateTransitionException>()
            .WithMessage("*receptor tax id*");
    }

    [Fact]
    public void Complete_WhenConsumptionBelow250000WithoutReceptorTaxId_TransitionsToCompleted()
    {
        var sale = CreateSale(items: [CreateSaleItem()], documentType: EcfDocumentType.Consumer, total: 249999.99m);

        sale.Complete();

        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public void Complete_WhenConsumptionOf250000OrMoreWithReceptorTaxId_TransitionsToCompleted()
    {
        var sale = CreateSale(items: [CreateSaleItem()], documentType: EcfDocumentType.Consumer, total: 250000m);
        sale.ReceptorTaxId = "101010101";

        sale.Complete();

        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public void Complete_WhenNoteWithoutReceptorTaxId_TransitionsToCompleted()
    {
        // A note takes the buyer from the document it modifies: it does not
        // name a buyer of its own.
        var sale = CreateSale(items: [CreateSaleItem()], documentType: EcfDocumentType.CreditNote, total: 300000m);

        sale.Complete();

        sale.Status.Should().Be(SaleStatus.Completed);
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

        sale.AddItem(productId: Guid7TestValues.ProductA, productName: "Product A", quantity: 2, unitPrice: 100, taxCode: TaxCode.ItbisTasa1);

        sale.Items.Should().HaveCount(1);
        sale.Items.First().ProductId.Should().Be(Guid7TestValues.ProductA);
        sale.Items.First().Quantity.Should().Be(2);
        sale.Items.First().UnitPrice.Should().Be(100);
        sale.Items.First().Subtotal.Should().Be(200);
    }

    [Fact]
    public void AddItem_CalculatesCorrectSubtotal()
    {
        var sale = CreateSale(items: []);

        sale.AddItem(productId: Guid7TestValues.ProductA, productName: "Product A", quantity: 3, unitPrice: 50, taxCode: TaxCode.ItbisTasa1);

        sale.Items.First().Subtotal.Should().Be(150);
    }

    [Fact]
    public void AddItem_WithSecondItem_IncreasesItemCount()
    {
        var sale = CreateSale(items:
        [
            new SaleItem { ProductId = Guid7TestValues.ProductA, ProductName = "A", Quantity = 1, UnitPrice = 10, Subtotal = 10 }
        ]);

        sale.AddItem(productId: Guid.NewGuid(), productName: "Product B", quantity: 3, unitPrice: 50, taxCode: TaxCode.ItbisTasa1);

        sale.Items.Should().HaveCount(2);
    }

    [Fact]
    public void AddItem_WithNullProductName_IsAllowed()
    {
        var sale = CreateSale(items: []);

        var act = () => sale.AddItem(productId: Guid7TestValues.ProductA, productName: null, quantity: 1, unitPrice: 100, taxCode: TaxCode.ItbisTasa1);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddItem_WithZeroQuantity_ThrowsArgumentException()
    {
        var sale = CreateSale(items: []);

        var act = () => sale.AddItem(productId: Guid7TestValues.ProductA, productName: "P", quantity: 0, unitPrice: 100, taxCode: TaxCode.ItbisTasa1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*quantity*");
    }

    [Fact]
    public void AddItem_WithNegativeUnitPrice_ThrowsArgumentException()
    {
        var sale = CreateSale(items: []);

        var act = () => sale.AddItem(productId: Guid7TestValues.ProductA, productName: "P", quantity: 1, unitPrice: -50, taxCode: TaxCode.ItbisTasa1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*price*");
    }

    // ===== RemoveItem =====

    [Fact]
    public void RemoveItem_ExistingItem_RemovesIt()
    {
        var sale = CreateSale(items:
        [
            new SaleItem { ProductId = Guid7TestValues.ProductA, ProductName = "A", Quantity = 2, UnitPrice = 100, Subtotal = 200 }
        ]);

        sale.RemoveItem(Guid7TestValues.ProductA);

        sale.Items.Should().HaveCount(0);
    }

    [Fact]
    public void RemoveItem_NonExistentProductId_IsNoOp()
    {
        var sale = CreateSale(items:
        [
            new SaleItem { ProductId = Guid7TestValues.ProductA, ProductName = "A", Quantity = 1, UnitPrice = 10, Subtotal = 10 }
        ]);

        var act = () => sale.RemoveItem(Guid.NewGuid());

        act.Should().NotThrow();
        sale.Items.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveItem_WithTaxedItem_RemovesItsItbisFromTheTotals()
    {
        var sale = CreateSale(items: [CreateSaleItem(taxCode: TaxCode.ItbisTasa1, subtotal: 100)]);
        sale.CalculateTotals();

        sale.RemoveItem(Guid7TestValues.SaleA);

        sale.Subtotal.Should().Be(0);
        sale.Itbis.Should().Be(0);
        sale.Total.Should().Be(0);
    }

    // ===== CalculateTotals =====

    [Fact]
    public void CalculateTotals_WithItbisTasa1Item_AddsEighteenPercent()
    {
        var sale = CreateSale(items: [CreateSaleItem(taxCode: TaxCode.ItbisTasa1, subtotal: 100)]);

        sale.CalculateTotals();

        sale.Subtotal.Should().Be(100);
        sale.Itbis.Should().Be(18);
        sale.Total.Should().Be(118);
    }

    [Fact]
    public void CalculateTotals_WithItbisTasa2Item_AddsSixteenPercent()
    {
        var sale = CreateSale(items: [CreateSaleItem(taxCode: TaxCode.ItbisTasa2, subtotal: 100)]);

        sale.CalculateTotals();

        sale.Subtotal.Should().Be(100);
        sale.Itbis.Should().Be(16);
        sale.Total.Should().Be(116);
    }

    [Theory]
    [InlineData(TaxCode.ItbisTasa3)]
    [InlineData(TaxCode.Exento)]
    [InlineData(TaxCode.NoFacturable)]
    public void CalculateTotals_WithUntaxedItem_AddsNoItbis(TaxCode taxCode)
    {
        var sale = CreateSale(items: [CreateSaleItem(taxCode: taxCode, subtotal: 100)]);

        sale.CalculateTotals();

        sale.Subtotal.Should().Be(100);
        sale.Itbis.Should().Be(0);
        sale.Total.Should().Be(100);
    }

    [Fact]
    public void CalculateTotals_WithMixedTaxCodes_AddsEachRate()
    {
        var sale = CreateSale(items:
        [
            CreateSaleItem(taxCode: TaxCode.ItbisTasa1, subtotal: 100),
            CreateSaleItem(taxCode: TaxCode.ItbisTasa2, subtotal: 100),
            CreateSaleItem(taxCode: TaxCode.Exento, subtotal: 100)
        ]);

        sale.CalculateTotals();

        sale.Subtotal.Should().Be(300);
        sale.Itbis.Should().Be(34);
        sale.Total.Should().Be(334);
    }

    [Fact]
    public void CalculateTotals_RoundsTheItbisOfEachItemToTwoDecimals()
    {
        var sale = CreateSale(items: [CreateSaleItem(taxCode: TaxCode.ItbisTasa1, subtotal: 33.33m)]);

        sale.CalculateTotals();

        sale.Itbis.Should().Be(6);
        sale.Total.Should().Be(39.33m);
    }

    // ===== Helpers =====

    private static Sale CreateSale(
        List<SaleItem>? items = null,
        SaleStatus status = SaleStatus.Pending,
        EcfDocumentType documentType = EcfDocumentType.Consumer,
        decimal total = 0)
    {
        return new Sale
        {
            Id = Guid7TestValues.SaleA,
            Status = status,
            DocumentType = documentType,
            Total = total,
            Items = items ?? [CreateSaleItem()]
        };
    }

    private static SaleItem CreateSaleItem(TaxCode taxCode = TaxCode.ItbisTasa1, decimal subtotal = 100)
    {
        return new SaleItem
        {
            ProductId = Guid7TestValues.SaleA,
            ProductName = "Test Product",
            Quantity = 1,
            UnitPrice = 100,
            Subtotal = subtotal,
            TaxCode = taxCode
        };
    }
}
