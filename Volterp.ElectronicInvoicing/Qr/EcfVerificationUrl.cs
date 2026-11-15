using System.Globalization;
using Volterp.ElectronicInvoicing.Dgii;

namespace Volterp.ElectronicInvoicing.Qr;

// The URLs a receiver opens to check a document. The summary of a consumption
// invoice below RD$250,000 does not travel to the taxpayer host: it goes to
// the consumption one, with the environment as its segment.
public static class EcfVerificationUrl
{
    // The consumption host of the DGII ("Descripción Técnica Servicios DGII",
    // p. 19 and p. 42).
    public const string ConsumptionHost = "https://fc.dgii.gov.do";

    // The host of the taxpayer ("Descripción Técnica Servicios DGII", p. 40):
    // the timbre of a document that is not a summary is answered there.
    public const string TaxpayerHost = "https://ecf.dgii.gov.do";

    // p. 20: GET {host}/{ambiente}/consultarfce/api/Consultas/Consulta
    //      ?RNC_Emisor=&ENCF=&Cod_Seguridad_eCF=
    // The consultation of a summary asks the DGII for its verdict, so it
    // travels with the session token.
    public static string ForConsumerInquiry(
        DgiiEnvironment environment,
        string issuerTaxId,
        string encf,
        string securityCode,
        string host = ConsumptionHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerTaxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encf);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityCode);

        return $"{Base(host, environment)}/consultarfce/api/Consultas/Consulta"
               + $"?RNC_Emisor={Uri.EscapeDataString(issuerTaxId)}"
               + $"&ENCF={Uri.EscapeDataString(encf)}"
               + $"&Cod_Seguridad_eCF={Uri.EscapeDataString(securityCode)}";
    }

    // p. 42: {host}/{ambiente}/consultatimbrefc
    //      ?rncemisor=&encf=&montototal=&codigoseguridad=
    // The timbre is what the QR of the printed representation carries: it is
    // opened by anyone, without a token.
    public static string ForRfceTimbre(
        DgiiEnvironment environment,
        string issuerTaxId,
        string encf,
        decimal total,
        string securityCode,
        string host = ConsumptionHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerTaxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encf);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityCode);

        return $"{Base(host, environment)}/consultatimbrefc"
               + $"?rncemisor={Uri.EscapeDataString(issuerTaxId)}"
               + $"&encf={Uri.EscapeDataString(encf)}"
               + $"&montototal={total.ToString("0.00", CultureInfo.InvariantCulture)}"
               + $"&codigoseguridad={Uri.EscapeDataString(securityCode)}";
    }

    // p. 40-41: {host}/{ambiente}/consultatimbre
    //      ?rncemisor=&rnccomprador=&encf=&fechaemision=&montototal=&fechafirma=&codigoseguridad=
    // The timbre of a document that is not a summary is the URL the QR of
    // its printed representation carries: the DGII answers it on the host
    // of the taxpayer, and anyone can open it without a token.
    public static string ForTimbre(
        DgiiEnvironment environment,
        string issuerTaxId,
        string? buyerTaxId,
        string encf,
        decimal total,
        DateTime issuedAt,
        DateTime signedAt,
        string securityCode,
        string host = TaxpayerHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerTaxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(encf);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityCode);

        return $"{Base(host, environment)}/consultatimbre"
               + $"?rncemisor={Uri.EscapeDataString(issuerTaxId)}"
               + $"&rnccomprador={Uri.EscapeDataString(buyerTaxId ?? string.Empty)}"
               + $"&encf={Uri.EscapeDataString(encf)}"
               + $"&fechaemision={issuedAt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}"
               + $"&montototal={total.ToString("0.00", CultureInfo.InvariantCulture)}"
               + $"&fechafirma={signedAt.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture).Replace(" ", "%20")}"
               + $"&codigoseguridad={Uri.EscapeDataString(securityCode)}";
    }

    private static string Base(string host, DgiiEnvironment environment)
        => $"{host.TrimEnd('/')}/{environment.ToPathSegment()}";
}
