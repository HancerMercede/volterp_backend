using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Volterp.DgiiSimulator;

public sealed class DgiiSimulatorApplication : IAsyncDisposable
{
    // The password of the throwaway certificate the simulator signs its own
    // tokens with. It protects a key that only lives in memory.
    private const string EphemeralPassword = "dgii-simulator-ephemeral";

    private readonly WebApplication _application;

    private DgiiSimulatorApplication(WebApplication application, X509Certificate2 certificate, Uri baseAddress)
    {
        _application = application;
        Certificate = certificate;
        BaseAddress = baseAddress;
    }

    public X509Certificate2 Certificate { get; }

    public Uri BaseAddress { get; }

    public Task WaitForShutdownAsync() => _application.WaitForShutdownAsync();

    public static async Task<DgiiSimulatorApplication> StartAsync(
        DgiiSimulatorOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var certificate = CreateSigningCertificate();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(options.BaseUrl);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(sp => new SimulatorTokens(certificate, sp.GetRequiredService<DgiiSimulatorOptions>()));
        builder.Services.AddSingleton<SimulatorSeeds>();
        builder.Services.AddSingleton<SimulatorDocuments>();

        var application = builder.Build();
        SimulatorEndpoints.Map(application);

        await application.StartAsync(cancellationToken);

        var address = application.Urls.First();

        return new DgiiSimulatorApplication(application, certificate, new Uri(address));
    }

    /// <summary>
    /// The certificate the simulator signs its tokens with. It is generated
    /// here and kept in memory: no key of anybody else is in this project, and
    /// none is written to disk.
    /// </summary>
    private static X509Certificate2 CreateSigningCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp DGII Simulator",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pfx, EphemeralPassword),
            EphemeralPassword,
            X509KeyStorageFlags.EphemeralKeySet);
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
        Certificate.Dispose();
    }
}
