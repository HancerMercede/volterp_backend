namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// What the environment has to carry for the circuit to run. The simulator is
// forgiving: it does not check the signature and it does not know the
// issuer. The real DGII is not, so against it the issuer, the number and the
// taxpayer's own certificate are required.
internal static class DgiiEnvironmentRequirements
{
    public static IReadOnlyList<string> Missing(
        string? baseUrl,
        string? issuerRnc,
        string? encf,
        string? certificatePath)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            missing.Add("VOLTERP_DGII_BASE_URL");
            return missing;
        }

        if (!IsTheRealDgii(baseUrl))
            return missing;

        if (string.IsNullOrWhiteSpace(issuerRnc))
            missing.Add("VOLTERP_DGII_ISSUER_RNC");

        if (string.IsNullOrWhiteSpace(encf))
            missing.Add("VOLTERP_DGII_ENCF");

        if (string.IsNullOrWhiteSpace(certificatePath))
            missing.Add("VOLTERP_DGII_CERTIFICATE");

        return missing;
    }

    // The real DGII is the one that signs with the taxpayer's certificate: a
    // certificate made here proves nothing there.
    public static bool IsTheRealDgii(string? baseUrl)
        => baseUrl?.Contains("dgii.gov.do", StringComparison.OrdinalIgnoreCase) == true;
}
