using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Volterp.Domain.Entities;
using Volterp.ElectronicInvoicing;
using Volterp.ElectronicInvoicing.Abstractions;
using Volterp.ElectronicInvoicing.Dgii;
using Volterp.Infrastructure.Data;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The annulment of a range as the tenant sees it: the company comes from the
// token and never from the request.
[Collection("VolterpApiFactory")]
public class RangeAnnulmentEndpointTests
{
    [Fact]
    public async Task TheAnnulmentOfARangeOfAnotherTenantIsNotFound()
    {
        var (factory, dgii) = Factory();
        using (factory)
        {
            await SeedAsync(factory);

            using var client = factory.CreateClient();
            SetAuth(factory, client, Guid7TestValues.CompanyA);

            var response = await client.PostAsync(
                $"/api/ElectronicInvoiceSequences/{Seed.OtherTenantRangeId}/annul",
                null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            dgii.Annulments.Should().Be(0);
        }
    }

    [Fact]
    public async Task TheAnnulmentOfARangeWithNothingToAnnulIsBadRequest()
    {
        var (factory, dgii) = Factory();
        using (factory)
        {
            await SeedAsync(factory);

            using var client = factory.CreateClient();
            SetAuth(factory, client, Guid7TestValues.CompanyA);

            var response = await client.PostAsync(
                $"/api/ElectronicInvoiceSequences/{Seed.ExhaustedRangeId}/annul",
                null);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("no numbers left");
            dgii.Annulments.Should().Be(0);
        }
    }

    [Fact]
    public async Task TheAnnulmentOfARangeAnswersWithWhatTheDgiiSaid()
    {
        var (factory, dgii) = Factory();
        using (factory)
        {
            await SeedAsync(factory);

            using var client = factory.CreateClient();
            SetAuth(factory, client, Guid7TestValues.CompanyA);

            var response = await client.PostAsync(
                $"/api/ElectronicInvoiceSequences/{Seed.RangeId}/annul",
                null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("Anulacion realizada");
            body.Should().Contain("Las secuencias fueron anuladas correctamente");

            dgii.Annulments.Should().Be(1);
            dgii.LastAnnulment.Should().Contain("<SecuenciaeNCFDesde>E310000000006</SecuenciaeNCFDesde>");
            dgii.LastAnnulment.Should().Contain("<SecuenciaeNCFHasta>E310000000010</SecuenciaeNCFHasta>");

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();
            var range = await db.ElectronicInvoiceSequences
                .IgnoreQueryFilters()
                .SingleAsync(r => r.Id == Seed.RangeId);

            range.IsAnnulled.Should().BeTrue();
            range.CurrentSequence.Should().Be(range.To);
        }
    }

    private static void SetAuth(ApiFactory factory, HttpClient client, Guid companyId)
    {
        var token = JwtTestHelper.MintToken(factory, companyId, Guid7TestValues.UserA);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // The transport and the certificate of the issuer are the ones the tests
    // decide: the annulment never leaves the machine.
    private static (ApiFactory Factory, DgiiStub Dgii) Factory()
    {
        var dgii = new DgiiStub();
        var certificates = new Mock<ISigningCertificateProvider>();
        certificates.Setup(c => c.GetSigningCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCertificate());

        var factory = new ApiFactory(services =>
        {
            services.RemoveAll<IDgiiClient>();
            services.RemoveAll<ISigningCertificateProvider>();
            services.AddSingleton<IDgiiClient>(dgii);
            services.AddSingleton(certificates.Object);
        });

        factory.EnsureSchemaCreated();
        return (factory, dgii);
    }

    private static async Task SeedAsync(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        db.Companies.AddRange(
            new Company
            {
                Id = Guid7TestValues.CompanyA,
                Name = "Company A",
                TaxId = "130862346",
                Address = "1 A Street",
                LegalName = "Company A Legal",
                Email = "a@company.test",
                Phone = "555-0001",
                IsActive = true
            },
            new Company
            {
                Id = Guid7TestValues.CompanyB,
                Name = "Company B",
                TaxId = "130862354",
                Address = "2 B Street",
                LegalName = "Company B Legal",
                Email = "b@company.test",
                Phone = "555-0002",
                IsActive = true
            });

        db.ElectronicInvoiceSequences.AddRange(
            new ElectronicInvoiceSequence
            {
                Id = Seed.RangeId,
                CompanyId = Guid7TestValues.CompanyA,
                IssuerTaxId = "130862346",
                DocumentType = EcfDocumentType.FiscalCredit,
                From = 1,
                To = 10,
                ExpiresAt = new DateTime(2028, 12, 31),
                CurrentSequence = 5
            },
            new ElectronicInvoiceSequence
            {
                Id = Seed.ExhaustedRangeId,
                CompanyId = Guid7TestValues.CompanyA,
                IssuerTaxId = "130862346",
                DocumentType = EcfDocumentType.Consumer,
                From = 1,
                To = 50_000_000,
                CurrentSequence = 50_000_000
            },
            new ElectronicInvoiceSequence
            {
                Id = Seed.OtherTenantRangeId,
                CompanyId = Guid7TestValues.CompanyB,
                IssuerTaxId = "130862354",
                DocumentType = EcfDocumentType.FiscalCredit,
                From = 1,
                To = 10,
                ExpiresAt = new DateTime(2028, 12, 31),
                CurrentSequence = 5
            });

        await db.SaveChangesAsync();
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

    private static class Seed
    {
        public static readonly Guid RangeId = Guid.CreateVersion7();
        public static readonly Guid ExhaustedRangeId = Guid.CreateVersion7();
        public static readonly Guid OtherTenantRangeId = Guid.CreateVersion7();
    }

    private sealed class DgiiStub : IDgiiClient
    {
        public int Annulments { get; private set; }

        public string? LastAnnulment { get; private set; }

        public Task<string> GetSeedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("<SemillaModel><valor>abc</valor><fecha>2019-03-13T14:33:32-04:00</fecha></SemillaModel>");

        public Task<DgiiToken> RequestTokenAsync(string signedSeedXml, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiToken("jwt", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));

        public Task<DgiiReceipt> SendElectronicInvoiceAsync(string signedEcfXml, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiReceipt("track-1", null, null));

        public Task<DgiiStatus> GetStatusAsync(string trackId, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiStatus(
                trackId,
                DgiiVerdict.Accepted,
                "Aceptado",
                "130862346",
                "E310000000001",
                true,
                DateTimeOffset.UtcNow,
                []));

        public Task<DgiiTrackLookup> GetTrackIdAsync(string issuerTaxId, string encf, string token, CancellationToken cancellationToken = default)
            => Task.FromResult(new DgiiTrackLookup("track-1", "Aceptado", DateTimeOffset.UtcNow));

        public Task<DgiiRangeAnnulmentAnswer> AnnulRangesAsync(string signedAnecfXml, string token, CancellationToken cancellationToken = default)
        {
            Annulments++;
            LastAnnulment = signedAnecfXml;

            return Task.FromResult(new DgiiRangeAnnulmentAnswer(
                "130862346",
                "1",
                "Anulacion realizada",
                ["Las secuencias fueron anuladas correctamente"])
            { RawBody = "{\"codigo\":\"1\"}" });
        }
    }
}
