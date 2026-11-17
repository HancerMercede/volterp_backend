using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Domain;

// The credential of a client that rents the API: a public selector that says
// which credential is presented and a secret that is only known by the
// client. What is stored is the hash of the secret, never the secret.
public class ApiCredentialTests
{
    [Fact]
    public void TheIssuedCredentialHasAPublicSelectorAndASecret()
    {
        var issued = ApiCredential.Create(Guid7TestValues.CompanyA, "POS", null);

        issued.Credential.KeyId.Should().HaveLength(ApiCredential.KeyIdLength);
        issued.Credential.KeyId.Should().NotBeNullOrWhiteSpace();
        issued.Secret.Should().NotBeNullOrWhiteSpace();
        issued.Secret.Should().NotContain("=", "the secret travels in a header: it is base64url without padding");
        issued.Secret.Should().NotContain(issued.Credential.KeyId);
    }

    [Fact]
    public void WhatIsStoredIsTheHashOfTheSecretAndNotTheSecret()
    {
        var issued = ApiCredential.Create(Guid7TestValues.CompanyA, "POS", null);

        var expected = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Secret)))
            .ToLowerInvariant();

        issued.Credential.SecretHash.Should().Be(expected);
        issued.Credential.SecretHash.Should().NotContain(issued.Secret);
        issued.Credential.SecretHash.Should().HaveLength(64);
    }

    [Fact]
    public void TwoCredentialsNeverShareTheSelector()
    {
        var selectors = Enumerable
            .Range(0, 100)
            .Select(_ => ApiCredential.Create(Guid7TestValues.CompanyA, "POS", null).Credential.KeyId)
            .ToList();

        selectors.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TwoCredentialsNeverShareTheSecret()
    {
        var secrets = Enumerable
            .Range(0, 100)
            .Select(_ => ApiCredential.Create(Guid7TestValues.CompanyA, "POS", null).Secret)
            .ToList();

        secrets.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TheCredentialBelongsToTheCompanyThatIssuedIt()
    {
        var issued = ApiCredential.Create(Guid7TestValues.CompanyA, "POS", null);

        issued.Credential.CompanyId.Should().Be(Guid7TestValues.CompanyA);
        issued.Credential.Status.Should().Be(EntityStatus.Active);
    }

    [Fact]
    public void ACredentialWithoutAnExpiryNeverExpires()
    {
        var issued = ApiCredential.Create(Guid7TestValues.CompanyA, "POS", null);

        issued.Credential.ExpiresAt.Should().BeNull();
        issued.Credential.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void ACredentialThatPassedItsExpiryIsExpired()
    {
        var issued = ApiCredential.Create(
            Guid7TestValues.CompanyA, "POS", DateTime.UtcNow.AddMinutes(-1));

        issued.Credential.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void ACredentialThatStillHasTimeIsNotExpired()
    {
        var issued = ApiCredential.Create(
            Guid7TestValues.CompanyA, "POS", DateTime.UtcNow.AddMinutes(1));

        issued.Credential.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void TheRevocationStopsTheCredentialWithoutDeletingIt()
    {
        var issued = ApiCredential.Create(Guid7TestValues.CompanyA, "POS", null);

        issued.Credential.Revoke();

        issued.Credential.Status.Should().Be(EntityStatus.Inactive);
        issued.Credential.Id.Should().NotBe(Guid.Empty);
    }
}
