using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Abstractions;
using Volterp.Application.Credentials;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Volterp.Infrastructure.Services;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

public class SigningCertificateTenantIsolationTests
{
    private const string IssuerRnc = "130862346";
    private const string OtherIssuerRnc = "132620951";
    private const string Password = "volterp-test-password";

    [Fact]
    public async Task TheLookupByIssuerRnc_DoesNotCrossTheTenant()
    {
        await using var fixture = new TenantFixture();
        await fixture.InitializeAsync();

        await using var context = fixture.ContextFor(fixture.CompanyA);
        var repository = new CompanyCertificateRepository(context);

        var foreign = await repository.GetActiveByTaxIdAsync(OtherIssuerRnc);
        var own = await repository.GetActiveByTaxIdAsync(IssuerRnc);

        foreign.Should().BeNull("the certificate of another tenant must not be visible");
        own.Should().NotBeNull();
        own!.CompanyId.Should().Be(fixture.CompanyA);
    }

    [Fact]
    public async Task TheProvider_DoesNotSignWithTheCertificateOfAnotherTenant()
    {
        await using var fixture = new TenantFixture();
        await fixture.InitializeAsync();

        await using var context = fixture.ContextFor(fixture.CompanyA);
        var provider = new CompanySigningCertificateProvider(
            new UnitOfWork(context),
            new AesCredentialProtector(fixture.MasterKey));

        var act = () => provider.GetSigningCertificateAsync(OtherIssuerRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TheProvider_SignsWithTheCertificateOfItsOwnTenant()
    {
        await using var fixture = new TenantFixture();
        await fixture.InitializeAsync();

        await using var context = fixture.ContextFor(fixture.CompanyB);
        var provider = new CompanySigningCertificateProvider(
            new UnitOfWork(context),
            new AesCredentialProtector(fixture.MasterKey));

        using var certificate = await provider.GetSigningCertificateAsync(OtherIssuerRnc);

        certificate.HasPrivateKey.Should().BeTrue();
    }

    private static string CreatePfx()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Volterp Test Emisor, C=DO",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(365));

        return Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, Password));
    }

    private sealed class TenantFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        public Guid CompanyA { get; } = Guid.CreateVersion7();
        public Guid CompanyB { get; } = Guid.CreateVersion7();
        public string MasterKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        public TenantFixture() => _connection.Open();

        public VolterpDbContext ContextFor(Guid companyId)
        {
            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new VolterpDbContext(options, new FixedTenantContext(companyId));
        }

        public async Task InitializeAsync()
        {
            await using var context = ContextFor(CompanyA);
            await context.Database.EnsureCreatedAsync();

            context.Companies.Add(NewCompany(CompanyA, "Company A", IssuerRnc));
            context.Companies.Add(NewCompany(CompanyB, "Company B", OtherIssuerRnc));
            context.CompanyCertificates.Add(NewCertificate(CompanyA, IssuerRnc, DateTime.UtcNow.AddDays(10)));
            context.CompanyCertificates.Add(NewCertificate(CompanyB, OtherIssuerRnc, DateTime.UtcNow.AddDays(100)));

            await context.SaveChangesAsync();
        }

        private static Company NewCompany(Guid id, string name, string taxId)
            => new()
            {
                Id = id,
                Name = name,
                TaxId = taxId,
                Address = "Dirección",
                LegalName = name,
                Email = "a@test.local",
                Phone = "8090000000",
                IsActive = true
            };

        private CompanyCertificate NewCertificate(Guid companyId, string taxId, DateTime expiresAt)
        {
            var protector = new AesCredentialProtector(MasterKey);
            var envelope = new SigningCredential(CreatePfx(), Password).ToEnvelope();

            return new CompanyCertificate
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                Name = $"eCF {taxId}",
                TaxId = taxId,
                ExpiresAt = expiresAt,
                Status = EntityStatus.Active,
                EncryptedCredential = protector.Protect(companyId, envelope)
            };
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class FixedTenantContext(Guid companyId) : ITenantContext
    {
        public Guid? CompanyId => companyId;

        public bool IsUnrestricted => false;
    }
}
