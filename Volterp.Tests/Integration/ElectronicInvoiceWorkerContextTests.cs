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

// The worker runs without an HTTP request. The container still gives it the
// HttpTenantContext, so its queries have to carry the company by hand.
public class ElectronicInvoiceWorkerContextTests
{
    [Fact]
    public async Task TheWorkerSendsTheDocumentAndTheRetrackRecordsTheDgiiVerdict()
    {
        var (factory, dgii) = Factory();
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            await SeedAsync(factory, companyId, EcfStatus.Signed);

            using (var scope = factory.Services.CreateScope())
            {
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.Should().BeOfType<HttpTenantContext>();
                tenantContext.CompanyId.Should().BeNull();
                tenantContext.IsUnrestricted.Should().BeFalse();
            }

            await Worker(factory).ProcessPendingAsync(CancellationToken.None);

            var invoice = await InvoiceAsync(factory, companyId);
            invoice.Status.Should().Be(EcfStatus.Accepted,
                "the document sent by the queue cannot stay waiting for its verdict forever");
            dgii.SendRequests.Should().Be(1);
            dgii.StatusRequests.Should().Be(1);
        }
    }

    [Fact]
    public async Task TheRetrackOfOneCompanyDoesNotReachTheDocumentOfAnother()
    {
        var (factory, dgii) = Factory();
        using (factory)
        {
            var companyId = Guid.CreateVersion7();
            var otherCompanyId = Guid.CreateVersion7();
            await SeedAsync(factory, companyId, EcfStatus.Sent);
            await SeedAsync(factory, otherCompanyId, EcfStatus.Sent, "E320000000002", "10100000102");

            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IElectronicInvoiceService>();
            var result = await service.RetrackAsync(companyId);

            result.Should().BeOfType<Either<Error, int>.Right>();
            (await InvoiceAsync(factory, companyId)).Status.Should().Be(EcfStatus.Accepted);
            (await InvoiceAsync(factory, otherCompanyId)).Status.Should().Be(EcfStatus.Sent);
            dgii.StatusRequests.Should().Be(1);
        }
    }

    private static (ApiFactory Factory, DgiiStub Dgii) Factory()
    {
        var dgii = new DgiiStub();
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
        string issuerRnc = "10100000101")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
        var saleId = Guid.CreateVersion7();

        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = $"Empresa {companyId}",
            LegalName = $"Empresa {companyId} SRL",
            TaxId = issuerRnc,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@empresa.test"
        });
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
            IssuerTaxId = issuerRnc,
            SignedXml = "<ECF />",
            SignedAt = DateTime.UtcNow,
            TrackId = status == EcfStatus.Signed ? null : "track-1",
            SentAt = status == EcfStatus.Signed ? null : DateTime.UtcNow,
            Status = status,
            NextCheckAt = status == EcfStatus.Signed ? null : DateTime.UtcNow.AddMinutes(-1)
        });

        await db.SaveChangesAsync();
    }

    private static async Task<ElectronicInvoice> InvoiceAsync(ApiFactory factory, Guid companyId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        return await db.ElectronicInvoices
            .IgnoreQueryFilters()
            .SingleAsync(invoice => invoice.CompanyId == companyId);
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
            StatusRequests++;
            return Task.FromResult(new DgiiStatus(
                trackId,
                DgiiVerdict.Accepted,
                "Aceptado",
                "10100000101",
                "E320000000001",
                true,
                DateTimeOffset.UtcNow,
                []));
        }

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));
    }
}
