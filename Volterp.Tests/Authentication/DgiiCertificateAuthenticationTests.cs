using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Volterp.Api.Authentication;
using Volterp.Api.Controllers;
using Volterp.Tests.Integration;
using Xunit;

namespace Volterp.Tests.Authentication;

public class DgiiCertificateAuthenticationTests
{
    private const string Scheme = ElectronicInvoiceReceptionController.DgiiAuthenticationScheme;
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    [Fact]
    public async Task AuthenticateAsync_WithoutACertificate_Fails()
    {
        var result = await AuthenticateAsync(null, _ => { });

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("certificate");
    }

    [Fact]
    public async Task AuthenticateAsync_WithACertificateOutsideTheConfiguredTrust_Fails()
    {
        using var authority = TestCertificateAuthority.Create();
        using var otherAuthority = TestCertificateAuthority.Create();
        using var certificate = otherAuthority.IssueClientCertificate();

        var result = await AuthenticateAsync(
            certificate,
            options => options.TrustedRootCertificates.Add(authority.Root));

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("trusted");
    }

    [Fact]
    public async Task AuthenticateAsync_WithAnExpiredCertificate_Fails()
    {
        using var authority = TestCertificateAuthority.Create();
        using var certificate = authority.IssueClientCertificate(
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1));

        var result = await AuthenticateAsync(
            certificate,
            options => options.TrustedRootCertificates.Add(authority.Root));

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("validity");
    }

    [Fact]
    public async Task AuthenticateAsync_WithACertificateThatCannotAuthenticateAClient_Fails()
    {
        using var authority = TestCertificateAuthority.Create();
        using var certificate = authority.IssueClientCertificate(
            extendedKeyUsageOid: ServerAuthenticationOid);

        var result = await AuthenticateAsync(
            certificate,
            options => options.TrustedRootCertificates.Add(authority.Root));

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("client authentication");
    }

    [Fact]
    public async Task AuthenticateAsync_WithAValidCertificateChainedToATrustedRoot_Succeeds()
    {
        using var authority = TestCertificateAuthority.Create();
        using var certificate = authority.IssueClientCertificate();

        var result = await AuthenticateAsync(
            certificate,
            options => options.TrustedRootCertificates.Add(authority.Root));

        result.Succeeded.Should().BeTrue();
        result.Ticket!.AuthenticationScheme.Should().Be(Scheme);
        result.Principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task Program_RegistersTheProductionCertificateHandler()
    {
        await using var factory = new ApiFactory();
        factory.EnsureSchemaCreated();
        var context = new DefaultHttpContext { RequestServices = factory.Services };

        var handler = await factory.Services
            .GetRequiredService<IAuthenticationHandlerProvider>()
            .GetHandlerAsync(context, Scheme);

        handler.Should().BeOfType<DgiiCertificateAuthenticationHandler>();
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        X509Certificate2? certificate,
        Action<DgiiCertificateAuthenticationOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication()
            .AddScheme<DgiiCertificateAuthenticationOptions, DgiiCertificateAuthenticationHandler>(
                Scheme,
                options =>
                {
                    options.RevocationMode = X509RevocationMode.NoCheck;
                    options.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;
                    configure(options);
                });

        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Connection.ClientCertificate = certificate;

        return await provider
            .GetRequiredService<IAuthenticationService>()
            .AuthenticateAsync(context, Scheme);
    }
}

internal sealed class TestCertificateAuthority : IDisposable
{
    private TestCertificateAuthority(X509Certificate2 root)
    {
        Root = root;
    }

    public X509Certificate2 Root { get; }

    public static TestCertificateAuthority Create()
    {
        var now = DateTimeOffset.UtcNow;
        using var rootKey = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=Volterp Test Certificate Authority"),
            rootKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(
            request.PublicKey,
            false));

        var root = request.CreateSelfSigned(now.AddDays(-10), now.AddYears(1));
        return new TestCertificateAuthority(root);
    }

    public X509Certificate2 IssueClientCertificate(
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        string extendedKeyUsageOid = "1.3.6.1.5.5.7.3.2")
    {
        var now = DateTimeOffset.UtcNow;
        using var leafKey = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName("CN=Volterp DGII Test Client"),
            leafKey,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new(extendedKeyUsageOid) },
            false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(
            request.PublicKey,
            false));

        return request.Create(
            Root,
            notBefore ?? now.AddMinutes(-5),
            notAfter ?? now.AddDays(30),
            RandomNumberGenerator.GetBytes(16));
    }

    public void Dispose() => Root.Dispose();
}
