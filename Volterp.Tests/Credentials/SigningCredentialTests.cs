using FluentAssertions;
using Volterp.Application.Credentials;
using Xunit;

namespace Volterp.Tests.Credentials;

public class SigningCredentialTests
{
    private const string PfxBase64 = "cGRmeA==";
    private const string Password = "volterp-test-password";

    [Fact]
    public void ToEnvelope_ThenParse_RoundTripsTheMaterial()
    {
        var credential = new SigningCredential(PfxBase64, Password);

        var parsed = SigningCredential.Parse(credential.ToEnvelope());

        parsed.PfxBase64.Should().Be(PfxBase64);
        parsed.Password.Should().Be(Password);
    }

    [Fact]
    public void ToEnvelope_UsesTheDocumentedShape()
    {
        var envelope = new SigningCredential(PfxBase64, Password).ToEnvelope();

        envelope.Should().Be("{\"pfx\":\"cGRmeA==\",\"password\":\"volterp-test-password\"}");
    }

    [Fact]
    public void Parse_WithAMalformedEnvelope_Throws()
    {
        var act = () => SigningCredential.Parse("not-an-envelope");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_WithoutThePassword_Throws()
    {
        var act = () => SigningCredential.Parse("{\"pfx\":\"cGRmeA==\"}");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_WithoutThePfx_Throws()
    {
        var act = () => SigningCredential.Parse($"{{\"password\":\"{Password}\"}}");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parse_WithAPfxThatIsNotBase64_Throws()
    {
        var act = () => SigningCredential.Parse("{\"pfx\":\"not base64!\",\"password\":\"secret\"}");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryParse_WithAnEmptyCredential_SaysWhyItIsNotValid()
    {
        var valid = SigningCredential.TryParse(" ", out _, out var error);

        valid.Should().BeFalse();
        error.Should().Be("The credential is empty.");
    }

    [Fact]
    public void TryParse_WithAValidEnvelope_AcceptsIt()
    {
        var valid = SigningCredential.TryParse(new SigningCredential(PfxBase64, Password).ToEnvelope(), out var credential, out var error);

        valid.Should().BeTrue();
        error.Should().BeNull();
        credential.PfxBase64.Should().Be(PfxBase64);
        credential.Password.Should().Be(Password);
    }
}
