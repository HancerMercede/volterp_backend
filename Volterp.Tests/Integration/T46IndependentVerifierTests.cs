using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using EitherWay;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Volterp.Api.Http;
using Volterp.Api.Workers;
using Volterp.Application.Abstractions;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Integration;

// The cycle of the verdict with the real container: the worker runs without
// an HTTP request, so the queries of the queue and of the retrack have to
// carry the company by hand. The probes of the independent verifier are here:
// they are the blockers that the green suite did not see.
[Collection("VolterpApiFactory")]
public class T46IndependentVerifierTests
{
    private const string IssuerRnc = "10100000101";
    private const string RejectionReason = "El RNC del receptor no existe.";

    [Fact]
    public async Task TheRetrackSeesTheDocumentsOfTheWorkerWithTheRealTenantContext()
    {
        var (factory, dgii) = Factory();
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            await SeedAsync(factory, companyId, EcfStatus.Sent);

            using (var scope = factory.Services.CreateScope())
            {
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

                // The worker has no request behind it: the context is there,
                // but it carries no company.
                tenantContext.Should().BeOfType<HttpTenantContext>();
                tenantContext.CompanyId.Should().BeNull();
                tenantContext.IsUnrestricted.Should().BeFalse();
            }

            using var serviceScope = factory.Services.CreateScope();
            var service = serviceScope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();

            var result = await service.RetrackAsync(companyId);

            result.Should().BeOfType<Either<Error, int>.Right>();
            dgii.StatusRequests.Should().Be(1,
                "the retrack has to see the documents of the worker: the filter of the tenant leaves none");
            (await InvoiceAsync(factory, companyId)).Status.Should().Be(EcfStatus.Accepted);
        }
    }

    [Fact]
    public async Task TheCycleEndsWithTheVerdictOfTheDgiiAndNotInSent()
    {
        var (factory, dgii) = Factory();
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            await SeedAsync(factory, companyId, EcfStatus.Signed);

            await Worker(factory).ProcessPendingAsync(CancellationToken.None);

            var invoice = await InvoiceAsync(factory, companyId);

            // A document that is sent and never asked about again is a hole
            // in the fiscal trail of the client.
            invoice.Status.Should().Be(EcfStatus.Accepted);
            invoice.TrackId.Should().NotBeNullOrWhiteSpace();
            dgii.SendRequests.Should().Be(1);
            dgii.StatusRequests.Should().Be(1);
        }
    }

    [Fact]
    public async Task ARejectionIsRecordedWithItsReason()
    {
        var dgii = new DgiiStub { Verdict = DgiiVerdict.Rejected, Reason = RejectionReason };
        var (factory, _) = Factory(dgii);
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            await SeedAsync(factory, companyId, EcfStatus.Sent);

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();

            await service.RetrackAsync(companyId);

            var invoice = await InvoiceAsync(factory, companyId);

            invoice.Status.Should().Be(EcfStatus.Rejected);
            invoice.VerdictNote.Should().Be(RejectionReason,
                "the tenant has to know why the DGII refused the document");
        }
    }

    [Fact]
    public async Task OneDocumentThatFailsDoesNotStopTheOthers()
    {
        var dgii = new DgiiStub { FailingTrackId = "track-failed" };
        var (factory, _) = Factory(dgii);
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            await SeedCompanyAsync(factory, companyId);
            await SeedDocumentAsync(factory, companyId, EcfStatus.Sent, "E320000000001", "track-failed");
            await SeedDocumentAsync(factory, companyId, EcfStatus.Sent, "E320000000002", "track-processed");

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();

            var result = await service.RetrackAsync(companyId);

            result.Should().BeOfType<Either<Error, int>.Right>();

            var processed = await InvoiceAsync(factory, companyId, "E320000000002");
            processed.Status.Should().Be(EcfStatus.Accepted);

            // The fallback of the pass: the document that failed keeps its
            // reason and gets another appointment.
            var failed = await InvoiceAsync(factory, companyId, "E320000000001");
            failed.Status.Should().Be(EcfStatus.Sent);
            failed.LastError.Should().Contain("The DGII did not answer.");
            failed.NextCheckAt.Should().NotBeNull("a failed check is tried again later");
        }
    }

    [Fact]
    public async Task ADocumentTheDgiiNeverReceivedIsNotSentAgain()
    {
        var dgii = new DgiiStub { TheDgiiKnowsTheDocument = false };
        var (factory, _) = Factory(dgii);
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            await SeedAsync(factory, companyId, EcfStatus.InProcess, trackId: null);

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();

            var result = await service.RetrackAsync(companyId);

            result.Should().BeOfType<Either<Error, int>.Right>();

            // The answer was lost: the DGII is asked whether it has the
            // document instead of sending it again.
            dgii.TrackIdRequests.Should().Be(1);
            dgii.SendRequests.Should().Be(0, "a document the DGII never received is not sent blind");

            var invoice = await InvoiceAsync(factory, companyId);
            invoice.Status.Should().Be(EcfStatus.InProcess);
        }
    }

    private static (ApiFactory Factory, DgiiStub Dgii) Factory(DgiiStub? dgii = null)
    {
        dgii ??= new DgiiStub();

        var certificates = new Mock<ISigningCertificateProvider>();
        certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());

        var signer = new Mock<IEcfSigner>();
        signer.Setup(s => s.Sign(It.IsAny<string>(), It.IsAny<X509Certificate2>()))
            .Returns("<SemillaModel />");

        var factory = new ApiFactory(services =>
        {
            services.RemoveAll<IDgiiClient>();
            services.RemoveAll<ISigningCertificateProvider>();
            services.RemoveAll<IEcfSigner>();
            services.AddSingleton<IDgiiClient>(dgii);
            services.AddSingleton(certificates.Object);
            services.AddSingleton(signer.Object);
        });

        factory.EnsureSchemaCreated();
        return (factory, dgii);
    }

    private static async Task SeedAsync(
        ApiFactory factory,
        Guid companyId,
        EcfStatus status,
        string encf = "E320000000001",
        string? trackId = "track-1")
    {
        await SeedCompanyAsync(factory, companyId);
        await SeedDocumentAsync(factory, companyId, status, encf, trackId);
    }

    private static async Task SeedCompanyAsync(ApiFactory factory, Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = $"Empresa {companyId}",
            LegalName = $"Empresa {companyId} SRL",
            TaxId = IssuerRnc,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@empresa.test"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDocumentAsync(
        ApiFactory factory,
        Guid companyId,
        EcfStatus status,
        string encf,
        string? trackId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var saleId = Guid.CreateVersion7();

        db.Sales.Add(new Sale
        {
            Id = saleId,
            CompanyId = companyId,
            Code = $"V-{encf}",
            Status = SaleStatus.Completed,
            DocumentType = EcfDocumentType.Consumer,
            Subtotal = 200m,
            Itbis = 36m,
            Total = 236m,
            CreatedAt = DateTime.UtcNow
        });
        db.ElectronicInvoices.Add(new ElectronicInvoice
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            SaleId = saleId,
            ENCF = encf,
            DocumentType = EcfDocumentType.Consumer,
            IssuerTaxId = IssuerRnc,
            SignedXml = "<ECF />",
            SignedAt = DateTime.UtcNow,
            TrackId = trackId,
            SentAt = trackId is null ? null : DateTime.UtcNow,
            Status = status,
            NextCheckAt = DateTime.UtcNow.AddMinutes(-1)
        });

        await db.SaveChangesAsync();
    }

    private static async Task<ElectronicInvoice> InvoiceAsync(
        ApiFactory factory, Guid companyId, string? encf = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        return await db.ElectronicInvoices
            .IgnoreQueryFilters()
            .SingleAsync(invoice => invoice.CompanyId == companyId
                && (encf == null || invoice.ENCF == encf));
    }

    private static ElectronicInvoiceQueueWorker Worker(ApiFactory factory)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Production");

        return new ElectronicInvoiceQueueWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            environment.Object,
            NullLogger<ElectronicInvoiceQueueWorker>.Instance);
    }

    private static X509Certificate2 CreateCertificate()
    {
        const string password = "volterp-test-password";

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp Test Emisor, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pfx, password),
            password,
            X509KeyStorageFlags.EphemeralKeySet);
    }

    private sealed class DgiiStub : IDgiiClient
    {
        public int SendRequests { get; private set; }
        public int StatusRequests { get; private set; }
        public int TrackIdRequests { get; private set; }

        public DgiiVerdict Verdict { get; set; } = DgiiVerdict.Accepted;
        public string? Reason { get; set; }
        public string? FailingTrackId { get; set; }
        public bool TheDgiiKnowsTheDocument { get; set; } = true;

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
        {
            SendRequests++;
            return Task.FromResult(new DgiiReceipt("track-1", null, null));
        }

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
        {
            if (trackId == FailingTrackId)
                throw new HttpRequestException("The DGII did not answer.");

            StatusRequests++;

            return Task.FromResult(new DgiiStatus(
                trackId,
                Verdict,
                Verdict.ToString(),
                IssuerRnc,
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                Reason is null ? [] : [new DgiiMessage(Reason, 1001)]));
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
        {
            TrackIdRequests++;

            return Task.FromResult(TheDgiiKnowsTheDocument
                ? new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow)
                : new DgiiTrackLookup(null, null, null));
        }
    }
}
