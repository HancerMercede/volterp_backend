using FluentAssertions;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Validation;
using Volterp.ElectronicInvoicing.Xml;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Validation;

public class EcfDocumentValidatorTests
{
    [Theory]
    [InlineData(EcfDocumentType.FiscalCredit)]
    [InlineData(EcfDocumentType.Consumer)]
    [InlineData(EcfDocumentType.DebitNote)]
    [InlineData(EcfDocumentType.CreditNote)]
    [InlineData(EcfDocumentType.Purchase)]
    [InlineData(EcfDocumentType.MinorExpenses)]
    [InlineData(EcfDocumentType.SpecialRegimes)]
    [InlineData(EcfDocumentType.Governmental)]
    [InlineData(EcfDocumentType.Exportation)]
    [InlineData(EcfDocumentType.ExternalPayments)]
    public void Validate_AcceptsADocumentThatMeetsTheRulesOfItsType(EcfDocumentType documentType)
    {
        var violations = EcfDocumentValidator.Validate(BuildDocument(documentType));

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_AcceptsACreditOnATypeThatHasNoPaymentTermsInItsSchema()
    {
        // The 43 does not carry <FechaLimitePago> nor <TerminoPago>, so a
        // credit on it has nothing to declare.
        var document = BuildDocument(EcfDocumentType.MinorExpenses) with
        {
            Identity = BuildDocument(EcfDocumentType.MinorExpenses).Identity with
            {
                PaymentType = EcfPaymentType.Credit,
                PaymentDueDate = null,
                PaymentTerm = null
            }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().NotContain(violation => violation.Rule == "payment-terms");
    }

    [Fact]
    public void Validate_RejectsATypeThatRequiresTheBuyerAndTheDocumentHasNone()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with { Buyer = null };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "buyer");
    }

    [Fact]
    public void Validate_RejectsATypeThatRequiresTheTaxIdOfTheBuyerAndTheDocumentHasNone()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Buyer = new EcfBuyer { BusinessName = "Cliente de Prueba SRL" }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "buyer.tax-id");
    }

    [Fact]
    public void Validate_RejectsATypeThatRequiresTheNameOfTheBuyerAndTheDocumentHasNone()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Buyer = new EcfBuyer { TaxId = "10100000999" }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "buyer.name");
    }

    [Fact]
    public void Validate_RejectsATypeThatRequiresTheExpiryOfTheAuthorizedSequenceAndTheDocumentHasNone()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Identity = BuildDocument(EcfDocumentType.FiscalCredit).Identity with { SequenceExpiresAt = null }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "sequence-expiry");
    }

    [Fact]
    public void Validate_RejectsAnIncomeTheTypeDoesNotDeclare()
    {
        var document = BuildDocument(EcfDocumentType.MinorExpenses) with
        {
            Identity = BuildDocument(EcfDocumentType.MinorExpenses).Identity with
            {
                TypeIncome = EcfTypeIncome.Operations
            }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "type-income");
    }

    [Fact]
    public void Validate_RejectsAnIncomeTheTypeDoesNotList()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Identity = BuildDocument(EcfDocumentType.FiscalCredit).Identity with
            {
                TypeIncome = (EcfTypeIncome)7
            }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "type-income");
    }

    [Fact]
    public void Validate_RejectsAPaymentTheTypeDoesNotAccept()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Identity = BuildDocument(EcfDocumentType.FiscalCredit).Identity with
            {
                PaymentType = (EcfPaymentType)9
            }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "payment-type");
    }

    [Fact]
    public void Validate_RejectsACreditDocumentWithoutTheTermsOfThePayment()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Identity = BuildDocument(EcfDocumentType.FiscalCredit).Identity with
            {
                PaymentType = EcfPaymentType.Credit
            }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "payment-terms");
    }

    [Fact]
    public void Validate_AcceptsACreditDocumentThatSaysWhenThePaymentFallsDue()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Identity = BuildDocument(EcfDocumentType.FiscalCredit).Identity with
            {
                PaymentType = EcfPaymentType.Credit,
                PaymentDueDate = new DateTime(2026, 12, 4),
                PaymentTerm = "30 dias"
            }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsALineWhoseKindTheTypeDoesNotAccept()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Items = [BuildItem() with { Kind = (EcfItemKind)7 }]
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "item-kind");
    }

    [Fact]
    public void Validate_RejectsATotalThatIsNotTheSumOfTheLines()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Totals = BuildDocument(EcfDocumentType.FiscalCredit).Totals with { Total = 1200m }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "totals");
        violations[0].Message.Should().Contain("MontoTotal");
    }

    [Fact]
    public void Validate_RejectsATaxableTotalThatIsNotTheSumOfTheLines()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Totals = BuildDocument(EcfDocumentType.FiscalCredit).Totals with { TaxableTotal = 900m }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "totals");
        violations[0].Message.Should().Contain("MontoGravadoTotal");
    }

    [Fact]
    public void Validate_RejectsAnItbisTotalThatIsNotTheTaxOfTheLines()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Totals = BuildDocument(EcfDocumentType.FiscalCredit).Totals with { ItbisTotal = 100m }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "totals");
        violations[0].Message.Should().Contain("TotalITBIS");
    }

    [Fact]
    public void Validate_AcceptsADifferenceOfOneCent()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Totals = BuildDocument(EcfDocumentType.FiscalCredit).Totals with { Total = 1180.01m }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsADifferenceOfTwoCents()
    {
        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Totals = BuildDocument(EcfDocumentType.FiscalCredit).Totals with { Total = 1180.02m }
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().ContainSingle(violation => violation.Rule == "totals");
    }

    [Fact]
    public void Validate_AcceptsADocumentWhoseTotalsCarryTheDiscountOfTheDocument()
    {
        var items = new[] { BuildItem() };
        var adjustments = new[]
        {
            new EcfAdjustment
            {
                LineNumber = 1,
                Kind = EcfAdjustmentKind.Discount,
                Amount = 100m
            }
        };

        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Items = items,
            Adjustments = adjustments,
            Totals = EcfTotalsCalculator.Calculate(items, adjustments)
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Validate_RejectsADocumentWhoseTotalsForgetTheDiscountOfTheDocument()
    {
        var items = new[] { BuildItem() };
        var adjustments = new[]
        {
            new EcfAdjustment
            {
                LineNumber = 1,
                Kind = EcfAdjustmentKind.Discount,
                Amount = 100m
            }
        };

        var document = BuildDocument(EcfDocumentType.FiscalCredit) with
        {
            Items = items,
            Adjustments = adjustments
        };

        var violations = EcfDocumentValidator.Validate(document);

        violations.Should().Contain(violation => violation.Rule == "totals");
    }

    private static EcfDocument BuildDocument(EcfDocumentType documentType)
    {
        var profile = EcfTypeProfiles.For(documentType);
        var items = new[] { BuildItem() };

        return new EcfDocument
        {
            DocumentType = documentType,
            Identity = new EcfIdentity
            {
                ENCF = "B310000000000",
                SequenceExpiresAt = profile.RequiresSequenceExpiry ? new DateTime(2026, 12, 31) : null,
                TypeIncome = profile.TypeIncome,
                PaymentType = EcfPaymentType.Cash
            },
            Issuer = new EcfIssuer
            {
                TaxId = "10100000101",
                BusinessName = "Emisor de Prueba SRL",
                Address = "Calle Principal 1, Santo Domingo",
                IssuedAt = new DateTime(2026, 11, 4)
            },
            Buyer = new EcfBuyer
            {
                TaxId = "10100000999",
                BusinessName = "Cliente de Prueba SRL"
            },
            Totals = EcfTotalsCalculator.Calculate(items),
            Items = items,
            SignedAt = new DateTime(2026, 11, 4, 10, 30, 0)
        };
    }

    private static EcfItem BuildItem() => new()
    {
        LineNumber = 1,
        TaxTreatment = TaxCode.ItbisTasa1,
        Name = "Producto de prueba",
        Kind = EcfItemKind.Goods,
        Quantity = 1m,
        UnitPrice = 1000m,
        Amount = 1000m
    };
}
