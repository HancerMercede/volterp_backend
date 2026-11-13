using Volterp.ElectronicInvoicing.Dgii;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// A test that only runs when the DGII is configured. The circuit against the
// real DGII needs the base URL and the issuer's certificate, and neither
// belongs in the repository: they come from the environment.
public sealed class DgiiEnvironmentFactAttribute : FactAttribute
{
    public DgiiEnvironmentFactAttribute()
    {
        if (DgiiEnvironmentSettings.BaseUrl is null || DgiiEnvironmentSettings.CertificatePath is null)
            Skip = "The DGII environment is not configured.";
    }
}

// Where the circuit runs and with which certificate. The names are the ones
// the environment carries.
internal static class DgiiEnvironmentSettings
{
    public static string? BaseUrl => System.Environment.GetEnvironmentVariable("VOLTERP_DGII_BASE_URL");

    public static string? CertificatePath => System.Environment.GetEnvironmentVariable("VOLTERP_DGII_CERTIFICATE");

    public static string? CertificatePassword => System.Environment.GetEnvironmentVariable("VOLTERP_DGII_CERTIFICATE_PASSWORD");

    public static DgiiEnvironment Environment =>
        Enum.TryParse(System.Environment.GetEnvironmentVariable("VOLTERP_DGII_ENVIRONMENT"), true, out DgiiEnvironment environment)
            ? environment
            : DgiiEnvironment.PreCertification;
}
