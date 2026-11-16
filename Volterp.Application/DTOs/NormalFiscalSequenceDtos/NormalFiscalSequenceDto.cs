using Volterp.Domain.Entities;
using Volterp.Domain.Enums;

namespace Volterp.Application.DTOs.NormalFiscalSequenceDtos;

public record NormalFiscalSequenceDto : IMapFrom<NormalFiscalSequence>
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string IssuerTaxId { get; set; } = string.Empty;
    public NormalFiscalDocumentType DocumentType { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int CurrentSequence { get; set; }
    public bool HasRoom { get; set; }

    public void MapFrom(NormalFiscalSequence source)
    {
        Id = source.Id;
        CompanyId = source.CompanyId;
        IssuerTaxId = source.IssuerTaxId;
        DocumentType = source.DocumentType;
        From = source.From;
        To = source.To;
        ExpiresAt = source.ExpiresAt;
        CurrentSequence = source.CurrentSequence;
        HasRoom = source.HasRoom;
    }
}
