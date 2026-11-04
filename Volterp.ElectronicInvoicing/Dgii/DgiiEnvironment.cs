namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// The three environments the DGII exposes. They differ only by the path segment,
/// so the whole client switches between them with a single value.
/// </summary>
public enum DgiiEnvironment
{
    /// <summary>Pre-certification. The only one where the DGII enables test sequences.</summary>
    PreCertification,

    /// <summary>Certification. Used while a taxpayer is going through the certification process.</summary>
    Certification,

    /// <summary>Production.</summary>
    Production
}

public static class DgiiEnvironmentExtensions
{
    /// <summary>
    /// The path segment the DGII uses for the environment: testecf, certecf or ecf.
    /// </summary>
    public static string ToPathSegment(this DgiiEnvironment environment) => environment switch
    {
        DgiiEnvironment.PreCertification => "testecf",
        DgiiEnvironment.Certification => "certecf",
        DgiiEnvironment.Production => "ecf",
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Unknown DGII environment.")
    };
}
