using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Xml;

namespace Volterp.Application.DTOs.ElectronicInvoiceSequenceDtos;

// Lo que el contribuyente sabe de su rango autorizado: hasta donde llega, cuando
// vence y en que numero va. El numero en el que va incluye los comprobantes
// normales que ya emitio en papel, porque el rango es el mismo.
public record SaveElectronicInvoiceSequenceDto(
    string IssuerTaxId,
    EcfDocumentType DocumentType,
    int From,
    int To,
    DateTime? ExpiresAt,
    int Position) : IMapTo<ElectronicInvoiceSequence>
{
    public ElectronicInvoiceSequence MapTo()
        => new()
        {
            IssuerTaxId = IssuerTaxId,
            DocumentType = DocumentType,
            From = From,
            To = To,
            ExpiresAt = ExpiresAt,
            CurrentSequence = Position
        };

    // Los tipos 32 y 34 no llevan fecha de vencimiento de secuencia.
    public bool RequiresExpiry => EcfTypeProfiles.For(DocumentType).RequiresSequenceExpiry;
}
