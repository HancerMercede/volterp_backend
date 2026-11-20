using FluentAssertions;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.DTOs;

// The fiscal note of a sale is the verdict of its document: text that came
// from outside, that cannot carry a credential out.
public class SaleDtoTests
{
    private const string TokenOfTheDgii = "eyJhbGciOiJIUzI1NiJ9.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    [Fact]
    public void TheMapperKeepsTheFiscalNoteOfTheSale()
    {
        var dto = new SaleDto();

        dto.MapFrom(SaleWithAVerdict("El RNC del receptor no existe."));

        dto.FiscalNote.Should().Be("El RNC del receptor no existe.");
    }

    [Fact]
    public void TheMapperDoesNotLetTheTokenOfTheDgiiOut()
    {
        var dto = new SaleDto();

        dto.MapFrom(SaleWithAVerdict($"{{\"token\":\"{TokenOfTheDgii}\",\"expira\":\"2026-11-14T11:00:00\"}}"));

        dto.FiscalNote.Should().NotContain(TokenOfTheDgii);
        dto.FiscalNote.Should().Contain("[redacted]");
    }

    [Fact]
    public void ASaleWithoutADocumentHasNoFiscalNote()
    {
        var dto = new SaleDto();

        dto.MapFrom(new Sale { Id = Guid.CreateVersion7(), Code = "V-0001" });

        dto.FiscalNote.Should().BeNull();
    }

    private static Sale SaleWithAVerdict(string verdictNote)
    {
        var invoice = new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            ENCF = "E320000000001",
            IssuerTaxId = "130862346",
            VerdictNote = verdictNote,
            Status = EcfStatus.Rejected
        };

        var sale = new Sale
        {
            Id = Guid.CreateVersion7(),
            Code = "V-0001",
            Status = SaleStatus.Completed
        };

        sale.ElectronicInvoices.Add(invoice);

        return sale;
    }
}
