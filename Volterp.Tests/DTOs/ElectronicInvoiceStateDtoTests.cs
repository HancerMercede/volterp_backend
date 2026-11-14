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
