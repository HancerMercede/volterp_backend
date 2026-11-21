namespace Volterp.Application.DTOs.ElectronicInvoiceDtos;

// The readiness of the taxpayer: what it needs to have to bill right now.
// The front used to deduce it from three screens, and the user that cannot
// read the difference pays for it with a document the DGII rejects.
//
// The system is modular: the taxpayer that invoices electronically has a
// certificate and a range of e-NCF, and the one that does not has a range
// of NCF and bills on paper. The readiness follows the path the sale takes
// today, so the taxpayer that never issues an e-CF is not told that it is
// missing something.
public record FiscalReadinessDto
{
    // The mode the taxpayer declared to the DGII: Electronic or Normal.
    public string EmissionMode { get; init; } = "Electronic";

    // The path the sale takes right now: a taxpayer in contingency issues
    // on paper even when it declared the electronic mode.
    public bool UsesElectronicEmission { get; init; }

    public bool IsInContingency { get; init; }

    // A taxpayer that issues on paper does not need a certificate.
    public bool HasValidCertificate { get; init; }

    public DateTime? CertificateExpiresAt { get; init; }

    // The taxpayer can bill right now: the path is open and the consumption
    // document has numbers left. The consumption is the one every sale can
    // issue, so it is the minimum.
    public bool IsReady { get; init; }

    // What is missing: the reason the taxpayer is not ready is the first
    // thing the front has to show. The mode is not a blocker: billing
    // normally is a right, not a fault.
    public IReadOnlyList<FiscalBlockerDto> Blockers { get; init; } = [];

    // The types the taxpayer can issue on the path it takes: the e-NCF ones
    // (E31, E32, E33, E34) or the NCF ones (B01, B02, B03, B04).
    public IReadOnlyList<FiscalReadinessDocumentTypeDto> DocumentTypes { get; init; } = [];
}

// One type the taxpayer can issue: the room its range has left and the
// reason it cannot be issued when it has none.
public record FiscalReadinessDocumentTypeDto
{
    // The code the taxpayer knows: E32 for the electronic consumption and
    // B02 for the one on paper.
    public string Code { get; init; } = string.Empty;

    // The official two-digit type code of the DGII: 32 for E32 and 2 for
    // B02.
    public int DocumentType { get; init; }

    public string Name { get; init; } = string.Empty;

    // The type can be issued right now: its range is alive, it is not
    // annulled and it has numbers left.
    public bool IsIssuable { get; init; }

    public int Remaining { get; init; }

    public DateTime? RangeExpiresAt { get; init; }

    // Why the type cannot be issued, when it cannot: the code the front
    // translates and the message for whoever reads the API.
    public string? BlockerCode { get; init; }

    public string? Blocker { get; init; }
}

// A reason the taxpayer cannot bill right now. The code is what the front
// translates: the message of the backend is in English and the screen of
// the cashier is not.
public record FiscalBlockerDto
{
    // "certificate-missing", "range-missing", "range-expired",
    // "range-exhausted", "range-annulled", "range-without-expiry".
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    // The code of the type the blocker speaks of ("B02"), when it speaks of
    // one. The front matches it with the types of the answer and does not
    // have to read the message to know which type it is.
    public string? DocumentTypeCode { get; init; }
}
