using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Domain;

public class ElectronicInvoiceSequenceTests
{
    [Fact]
    public void BuildEncf_WithFiscalCredit_ReturnsThirteenCharactersStartingWithE31()
    {
        var encf = ElectronicInvoiceSequence.BuildEncf(SaleDocumentType.FiscalCredit, 1);

        encf.Should().Be("E310000000001");
        encf.Should().HaveLength(13);
    }

    [Fact]
    public void BuildEncf_WithConsumer_ReturnsThirteenCharactersStartingWithE32()
    {
        var encf = ElectronicInvoiceSequence.BuildEncf(SaleDocumentType.Consumer, 42);

        encf.Should().Be("E320000000042");
    }

    [Fact]
    public void BuildEncf_WithTenDigitSequence_KeepsTheThirteenCharacterLength()
    {
        var encf = ElectronicInvoiceSequence.BuildEncf(SaleDocumentType.FiscalCredit, 1234567890);

        encf.Should().Be("E311234567890");
        encf.Should().HaveLength(13);
    }
}
