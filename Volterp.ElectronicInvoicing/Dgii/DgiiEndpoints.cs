namespace Volterp.ElectronicInvoicing.Dgii;

// The taxpayer services of the DGII ("Descripción Técnica Servicios DGII").
// The emisor-receptor family is a different contract: that one we publish,
// not the one we call.
public static class DgiiEndpoints
{
    // p. 10: GET {base}/{ambiente}/autenticacion/api/autenticacion/semilla
    public const string Seed = "autenticacion/api/autenticacion/semilla";

    // p. 11: POST {base}/{ambiente}/autenticacion/api/autenticacion/validarsemilla
    public const string ValidateSeed = "autenticacion/api/autenticacion/validarsemilla";

    // p. 13: POST {base}/{ambiente}/recepcion/api/facturaselectronicas
    public const string ReceiveElectronicInvoice = "recepcion/api/facturaselectronicas";

    // p. 22: GET {base}/{ambiente}/consultaresultado/api/consultas/estado?trackid=
    public const string StatusByTrackId = "consultaresultado/api/consultas/estado";
}
