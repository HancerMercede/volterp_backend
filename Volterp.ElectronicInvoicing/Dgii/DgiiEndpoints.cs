namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The paths of the DGII web services, as the "Descripción Técnica Servicios
/// DGII" (47 pages) documents them.
/// </summary>
/// <remarks>
/// The DGII publishes two families of services and they are NOT
/// interchangeable: the taxpayer services, which an issuer calls to emit, and
/// the emisor-receptor communication, which is the surface we publish so the
/// DGII can call us. Every constant below belongs to the first family.
/// </remarks>
public static class DgiiEndpoints
{
    /// <summary>
    /// The seed the issuer signs with its own certificate.
    /// "Descripción Técnica Servicios DGII", p. 10:
    /// GET {base}/{ambiente}/autenticacion/api/autenticacion/semilla
    /// </summary>
    public const string Seed = "autenticacion/api/autenticacion/semilla";

    /// <summary>
    /// The validation of the signed seed, which answers the token.
    /// "Descripción Técnica Servicios DGII", p. 11:
    /// POST {base}/{ambiente}/autenticacion/api/autenticacion/validarsemilla
    /// </summary>
    public const string ValidateSeed = "autenticacion/api/autenticacion/validarsemilla";

    /// <summary>
    /// The reception of the e-CF the issuer sends.
    /// "Descripción Técnica Servicios DGII", p. 13:
    /// POST {base}/{ambiente}/recepcion/api/facturaselectronicas
    /// </summary>
    public const string ReceiveElectronicInvoice = "recepcion/api/facturaselectronicas";

    /// <summary>
    /// The verdict of a sent e-CF, asked by its TrackId.
    /// "Descripción Técnica Servicios DGII", p. 22:
    /// GET {base}/{ambiente}/consultaresultado/api/consultas/estado?trackid=
    /// </summary>
    public const string StatusByTrackId = "consultaresultado/api/consultas/estado";
}
