using FluentAssertions;
using Volterp.Application.DTOs.ElectronicInvoiceDtos;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Xunit;

namespace Volterp.Tests.DTOs;

// The fiscal state of the surface is a classification, not a field of the
// document: the mapper has to make it, or the DTO answers a state that does
// not exist.
public class ElectronicInvoiceStateDtoTests
{
    [Fact]
    public void TheMapperClassifiesTheDocumentThatIsWaitingInTheQueue()
    {
        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(Document(EcfStatus.Signed, requiresReview: false));

        dto.FiscalState.Should().Be(EcfFiscalState.PendingEmission);
    }

    [Fact]
    public void TheMapperClassifiesTheDocumentThatTheDgiiHasNotValidated()
    {
        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(Document(EcfStatus.InProcess, requiresReview: false));

        dto.FiscalState.Should().Be(EcfFiscalState.InProcess);
    }

    [Fact]
    public void TheMapperClassifiesTheRejectedDocument()
    {
        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(Document(EcfStatus.Rejected, requiresReview: false));

        dto.FiscalState.Should().Be(EcfFiscalState.Rejected);
    }

    [Fact]
    public void TheMapperClassifiesTheDocumentThatCouldNotBeSigned()
    {
        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(Document(EcfStatus.Draft, requiresReview: true));

        dto.FiscalState.Should().Be(EcfFiscalState.RequiresReview);
    }

    [Fact]
    public void AClosedDocumentHasNoFiscalState()
    {
        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(Document(EcfStatus.Accepted, requiresReview: false));

        dto.FiscalState.Should().BeNull();
    }

    [Fact]
    public void AStateDtoThatWasNeverMappedHasNoFiscalState()
    {
        // The enum starts at one: the default of the property would be a
        // state that does not exist.
        new ElectronicInvoiceStateDto().FiscalState.Should().BeNull();
    }

    [Fact]
    public void TheMapperKeepsTheBodyOfTheDgiiWhenItFits()
    {
        const string body = """{"codigo":1,"estado":"Aceptado","mensajes":[{"valor":"Aceptado","codigo":1}]}""";
        var document = Document(EcfStatus.Accepted, requiresReview: false);
        document.RawResponse = body;

        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(document);

        // The operator has to see what the DGII answered: the codes of its
        // messages are in the body.
        dto.RawResponse.Should().Be(body);
    }

    [Fact]
    public void TheMapperCutsTheBodyOfTheDgiiWhenItIsPathological()
    {
        var document = Document(EcfStatus.Rejected, requiresReview: false);
        document.RecordDgiiResponse(new string('x', 50_000));

        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(document);

        // The body of the DGII is a diagnostic, not a payload: the row cannot
        // grow without limit.
        dto.RawResponse!.Length.Should().BeLessThan(50_000);

        // And the cut says so: the operator has to know that the answer he
        // is reading is not the whole answer.
        dto.RawResponse.Should().EndWith("[truncated]");
    }

    [Fact]
    public void TheTokenOfTheDgiiDoesNotComeOut()
    {
        var document = Document(EcfStatus.InProcess, requiresReview: false);
        document.RawResponse =
            """{"token":"eyJhbGciOiJIUzI1NiJ9.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk","expira":"2026-11-14T11:00:00"}""";

        var dto = new ElectronicInvoiceStateDto();

        dto.MapFrom(document);

        // The token of the DGII is a credential: the state of the tenant is
        // not the place where it travels.
        dto.RawResponse.Should().NotContain("eyJhbGciOiJIUzI1NiJ9");
        dto.RawResponse.Should().Contain("[redacted]");
    }

    private static ElectronicInvoice Document(EcfStatus status, bool requiresReview) => new()
    {
        Id = Guid.CreateVersion7(),
        CompanyId = Guid.CreateVersion7(),
        SaleId = Guid.CreateVersion7(),
        ENCF = "E320000000001",
        DocumentType = EcfDocumentType.Consumer,
        Status = status,
        RequiresReview = requiresReview
    };
}
