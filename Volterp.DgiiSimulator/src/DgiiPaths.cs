namespace Volterp.DgiiSimulator;

/// <summary>
/// The paths of the taxpayer services of the DGII, exactly as the
/// "Descripción Técnica Servicios DGII" writes them. They are the family the
/// taxpayer consumes; the emisor-receptor family is a different contract that
/// Volterp publishes, not one it calls.
/// </summary>
internal static class DgiiPaths
{
    // p. 10: GET {base}/{ambiente}/autenticacion/api/autenticacion/semilla
    public const string Seed = "/autenticacion/api/autenticacion/semilla";

    // p. 11: POST {base}/{ambiente}/autenticacion/api/autenticacion/validarsemilla
    public const string ValidateSeed = "/autenticacion/api/autenticacion/validarsemilla";

    // p. 13: POST {base}/{ambiente}/recepcion/api/facturaselectronicas
    public const string ReceiveElectronicInvoice = "/recepcion/api/facturaselectronicas";

    // p. 22: GET {base}/{ambiente}/consultaresultado/api/consultas/estado?trackid=
    public const string StatusByTrackId = "/consultaresultado/api/consultas/estado";

    // p. 30: GET {base}/{ambiente}/consultatrackids/api/trackids/consulta?rncemisor=&encf=
    public const string TrackIdByEncf = "/consultatrackids/api/trackids/consulta";

    // p. 27: GET {base}/{ambiente}/consultaestado/api/consultas/estado?rncemisor=&ncfelectronico=
    public const string DocumentState = "/consultaestado/api/consultas/estado";

    private static readonly string[] Known =
    [
        Seed,
        ValidateSeed,
        ReceiveElectronicInvoice,
        StatusByTrackId,
        TrackIdByEncf,
        DocumentState
    ];

    // The environments the DGII publishes, with the case it writes them in.
    private static readonly string[] Environments = ["testecf", "certecf", "ecf"];

    /// <summary>
    /// The router of ASP.NET matches the literals without regard for case, so
    /// a path that only differs in case would be answered anyway. The DGII's
    /// paths are lower case, and a client that gets them wrong has to find out
    /// here instead of in production.
    /// </summary>
    public static bool HasExactCasing(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return true;

        if (!Environments.Contains(segments[0], StringComparer.Ordinal))
            return false;

        var service = "/" + string.Join('/', segments.Skip(1));

        return Known.All(known => !string.Equals(known, service, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(known, service, StringComparison.Ordinal));
    }
}
