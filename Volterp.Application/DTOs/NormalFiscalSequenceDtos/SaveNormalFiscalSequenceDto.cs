using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.NormalFiscalSequenceDtos;

// Lo que el contribuyente sabe de su rango autorizado de comprobantes
// normales: hasta donde llega, cuando vence y en que numero va.
public record SaveNormalFiscalSequenceDto(
    string IssuerTaxId,
    NormalFiscalDocumentType DocumentType,
    int From,
    int To,
    DateTime? ExpiresAt,
    int Position) : IMapTo<NormalFiscalSequence>
{
    public NormalFiscalSequence MapTo()
        => new()
        {
            IssuerTaxId = IssuerTaxId,
            DocumentType = DocumentType,
            From = From,
            To = To,
            ExpiresAt = ExpiresAt,
            CurrentSequence = Position
        };

    // Los tipos que no llevan fecha de vencimiento de secuencia.
    public bool RequiresExpiry => NormalFiscalDocumentTypes.RequiresExpiry(DocumentType);
}
