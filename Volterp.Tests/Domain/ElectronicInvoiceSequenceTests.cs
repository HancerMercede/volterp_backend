using FluentAssertions;
using Volterp.Domain.Entities;
using Xunit;
using Volterp.ElectronicInvoicing;

namespace Volterp.Tests.Domain;

public class ElectronicInvoiceSequenceTests
{
    [Fact]
    public void BuildEncf_WithFiscalCredit_ReturnsThirteenCharactersStartingWithE31()
    {
        var encf = ElectronicInvoiceSequence.BuildEncf(EcfDocumentType.FiscalCredit, 1);

        encf.Should().Be("E310000000001");
        encf.Should().HaveLength(13);
    }

    [Fact]
    public void BuildEncf_WithConsumer_ReturnsThirteenCharactersStartingWithE32()
    {
        var encf = ElectronicInvoiceSequence.BuildEncf(EcfDocumentType.Consumer, 42);

        encf.Should().Be("E320000000042");
    }

    [Fact]
    public void BuildEncf_WithTenDigitSequence_KeepsTheThirteenCharacterLength()
    {
        var encf = ElectronicInvoiceSequence.BuildEncf(EcfDocumentType.FiscalCredit, 1234567890);

        encf.Should().Be("E311234567890");
        encf.Should().HaveLength(13);
    }

    [Fact]
    public void HasRoom_WithThePositionStillInsideTheRange_IsTrue()
    {
        var range = Range(EcfDocumentType.FiscalCredit, from: 100, to: 200, position: 150);

        range.HasRoom.Should().BeTrue();
    }

    [Fact]
    public void HasRoom_WithThePositionAtTheEndOfTheRange_IsFalse()
    {
        var range = Range(EcfDocumentType.FiscalCredit, from: 100, to: 200, position: 200);

        range.HasRoom.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithTheExpiryAlreadyPassed_IsTrue()
    {
        var range = Range(EcfDocumentType.FiscalCredit, expiresAt: new DateTime(2020, 12, 31));

        range.IsExpired(new DateTime(2026, 11, 13)).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WithTheExpiryStillAhead_IsFalse()
    {
        var range = Range(EcfDocumentType.FiscalCredit, expiresAt: new DateTime(2028, 12, 31));

        range.IsExpired(new DateTime(2026, 11, 13)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithATypeThatCarriesNoExpiry_IsFalse()
    {
        var range = Range(EcfDocumentType.Consumer, expiresAt: new DateTime(2020, 12, 31));

        range.IsExpired(new DateTime(2026, 11, 13)).Should().BeFalse();
    }

    private static ElectronicInvoiceSequence Range(
        EcfDocumentType documentType,
        int from = 1,
        int to = 100,
        int position = 0,
        DateTime? expiresAt = null) => new()
        {
            Id = Guid.CreateVersion7(),
            CompanyId = Guid.CreateVersion7(),
            IssuerTaxId = "10100000101",
            DocumentType = documentType,
            From = from,
            To = to,
            ExpiresAt = expiresAt,
            CurrentSequence = position
        };
}
