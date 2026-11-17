namespace Volterp.ElectronicInvoicing.Xml;

// The document that announces to the DGII the e-NCF numbers that a range will
// never use ("Formato Anulación de Secuencias de e-NCF v1.0"). It is signed
// with the certificate of the issuer, the same one that signs its e-CF.
public sealed record AnecfDocument
{
    // The RNC of the taxpayer that owns the ranges.
    public required string IssuerTaxId { get; init; }

    // The moment the document is generated.
    public required DateTime GeneratedAt { get; init; }

    // The ranges being annulled, one line per type of e-CF.
    public required IReadOnlyList<AnecfAnnulment> Annulments { get; init; }
}

// One line of the detail: the ranges of a single type of e-CF.
public sealed record AnecfAnnulment
{
    public required EcfDocumentType DocumentType { get; init; }

    public required IReadOnlyList<AnecfSequenceRange> Sequences { get; init; }
}

// A range of e-NCF that will not be used, with both of its ends written in
// full: the DGII reads the thirteen characters of the e-NCF.
public sealed record AnecfSequenceRange
{
    public required string From { get; init; }

    public required string To { get; init; }
}
