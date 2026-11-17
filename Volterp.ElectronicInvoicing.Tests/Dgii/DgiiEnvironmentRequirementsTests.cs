using FluentAssertions;
using Xunit;

namespace Volterp.ElectronicInvoicing.Tests.Dgii;

// The circuit against the real DGII only works with the taxpayer's own
// certificate, its RNC and a number of its own range. Without them the
// circuit would run against the simulator and look like it worked.
public class DgiiEnvironmentRequirementsTests
{
    [Fact]
    public void AgainstTheRealDgii_TheIssuerTheNumberAndTheCertificateAreRequired()
    {
        var missing = DgiiEnvironmentRequirements.Missing(
            baseUrl: "https://ecf.dgii.gov.do/ecf",
            issuerRnc: null,
            encf: null,
            certificatePath: null);

        missing.Should().Contain("VOLTERP_DGII_ISSUER_RNC");
        missing.Should().Contain("VOLTERP_DGII_ENCF");
        missing.Should().Contain("VOLTERP_DGII_CERTIFICATE");
    }

    [Fact]
    public void AgainstTheRealDgii_WithEverythingTheEnvironmentIsComplete()
    {
        var missing = DgiiEnvironmentRequirements.Missing(
            baseUrl: "https://ecf.dgii.gov.do/ecf",
            issuerRnc: "101500018",
            encf: "E320000000001",
            certificatePath: "C:/certificates/emisor.p12");

        missing.Should().BeEmpty();
    }

    [Fact]
    public void AgainstTheSimulator_OnlyTheBaseUrlIsRequired()
    {
        var missing = DgiiEnvironmentRequirements.Missing(
            baseUrl: "http://localhost:5080",
            issuerRnc: null,
            encf: null,
            certificatePath: null);

        missing.Should().BeEmpty();
    }

    [Fact]
    public void WithoutABaseUrl_TheCircuitDoesNotRun()
    {
        var missing = DgiiEnvironmentRequirements.Missing(
            baseUrl: null,
            issuerRnc: null,
            encf: null,
            certificatePath: null);

        missing.Should().Contain("VOLTERP_DGII_BASE_URL");
    }
}
