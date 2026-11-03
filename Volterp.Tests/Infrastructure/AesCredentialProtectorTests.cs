using System.Security.Cryptography;
using FluentAssertions;
using Volterp.Infrastructure.Services;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Infrastructure;

public class AesCredentialProtectorTests
{
    private const string MasterKey = "Vm9sdGVycERldk1hc3RlcktleV8yMDI2XzMyYnl0ZXM=";
    private const string Credential = "api-key-of-the-issuer";

    [Fact]
    public void Protect_ThenUnprotect_ReturnsTheOriginalCredential()
    {
        var protector = new AesCredentialProtector(MasterKey);

        var protectedValue = protector.Protect(Guid7TestValues.CompanyA, Credential);

        protector.Unprotect(Guid7TestValues.CompanyA, protectedValue).Should().Be(Credential);
    }

    [Fact]
    public void Protect_SameCredentialTwice_ProducesDifferentCiphertext()
    {
        var protector = new AesCredentialProtector(MasterKey);

        var first = protector.Protect(Guid7TestValues.CompanyA, Credential);
        var second = protector.Protect(Guid7TestValues.CompanyA, Credential);

        first.Should().NotBe(second);
    }

    [Fact]
    public void Protect_DoesNotLeaveTheCredentialInClear()
    {
        var protector = new AesCredentialProtector(MasterKey);

        var protectedValue = protector.Protect(Guid7TestValues.CompanyA, Credential);

        protectedValue.Should().NotContain(Credential);
    }

    [Fact]
    public void Unprotect_WithTheKeyOfAnotherTenant_Fails()
    {
        var protector = new AesCredentialProtector(MasterKey);
        var protectedValue = protector.Protect(Guid7TestValues.CompanyA, Credential);

        var act = () => protector.Unprotect(Guid7TestValues.CompanyB, protectedValue);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WithATamperedCiphertext_Fails()
    {
        var protector = new AesCredentialProtector(MasterKey);
        var protectedValue = protector.Protect(Guid7TestValues.CompanyA, Credential);

        var act = () => protector.Unprotect(Guid7TestValues.CompanyA, Tamper(protectedValue));

        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("not-a-base64-key!!")]
    [InlineData("c2hvcnQ=")]
    public void Constructor_WithAMasterKeyThatIsNotA256BitKey_Fails(string masterKey)
    {
        var act = () => new AesCredentialProtector(masterKey);

        act.Should().Throw<InvalidOperationException>();
    }

    private static string Tamper(string protectedValue)
    {
        var separator = protectedValue.IndexOf(':');
        var payload = Convert.FromBase64String(protectedValue[(separator + 1)..]);
        payload[^1] ^= 0xFF;

        return protectedValue[..(separator + 1)] + Convert.ToBase64String(payload);
    }
}
