using FluentAssertions;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.DTOs;

// The note of a verdict is text that came from outside: the conversation
// with the DGII carries its token, and what leaves the API never carries a
// credential.
public class ElectronicInvoiceDtoTests
{
    private const string TokenOfTheDgii = "eyJhbGciOiJIUzI1NiJ9.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    [Fact]
    public void TheMapperKeepsTheNoteOfTheVerdict()
    {
        var dto = new ElectronicInvoiceDto();

        dto.MapFrom(Document("El RNC del receptor no existe."));

        dto.VerdictNote.Should().Be("El RNC del receptor no existe.");
    }

    [Fact]
    public void TheMapperDoesNotLetTheBearerOfTheDgiiOut()
    {
        var dto = new ElectronicInvoiceDto();

        dto.MapFrom(Document($"The conversation failed: bearer {TokenOfTheDgii}"));

        dto.VerdictNote.Should().NotContain(TokenOfTheDgii);
        dto.VerdictNote.Should().Contain("[redacted]");
    }

    [Fact]
    public void TheMapperDoesNotLetTheTokenOfTheDgiiOut()
    {
        var dto = new ElectronicInvoiceDto();

        dto.MapFrom(Document($"{{\"token\":\"{TokenOfTheDgii}\",\"expira\":\"2026-11-14T11:00:00\"}}"));

        dto.VerdictNote.Should().NotContain(TokenOfTheDgii);
        dto.VerdictNote.Should().Contain("[redacted]");
    }

    [Fact]
    public void ANoteWithoutACredentialIsNotTouched()
    {
        var dto = new ElectronicInvoiceDto();

        dto.MapFrom(Document("El comprobante fue rechazado por el RNC del receptor."));

        dto.VerdictNote.Should().Be("El comprobante fue rechazado por el RNC del receptor.");
    }

    private static ElectronicInvoice Document(string verdictNote) => new ElectronicInvoice
    {
        Id = Guid.CreateVersion7(),
        CompanyId = Guid.CreateVersion7(),
        SaleId = Guid.CreateVersion7(),
        ENCF = "E320000000001",
        IssuerTaxId = "130862346",
        VerdictNote = verdictNote,
        Status = EcfStatus.Rejected
    };
}
