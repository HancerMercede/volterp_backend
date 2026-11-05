namespace Volterp.ElectronicInvoicing.Dgii;

/// <summary>
/// Where the DGII client talks to and which environment it talks to.
/// </summary>
public sealed class DgiiOptions
{
    /// <summary>The configuration section the host binds the options from.</summary>
    public const string SectionName = "Dgii";

    /// <summary>The DGII host. Every service lives under it, one path segment per service.</summary>
    public string BaseUrl { get; set; } = "https://ecf.dgii.gov.do";

    /// <summary>
    /// Pre-certification by default: sending to production before the client is
    /// certified would burn real sequences.
    /// </summary>
    public DgiiEnvironment Environment { get; set; } = DgiiEnvironment.PreCertification;
}
