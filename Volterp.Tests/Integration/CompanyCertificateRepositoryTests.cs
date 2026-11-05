using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

public class CompanyCertificateRepositoryTests
{
    private const string IssuerRnc = "130862346";
    private const string ExpiredIssuerRnc = "131123432";
    private const string InactiveIssuerRnc = "132620951";

    [Fact]
    public async Task GetActiveByTaxIdAsync_ReturnsTheActiveCertificateThatExpiresLast()
    {
        await using var fixture = new CertificateFixture();
        await fixture.InitializeAsync();

        var certificate = await fixture.Repository.GetActiveByTaxIdAsync(IssuerRnc);

        certificate.Should().NotBeNull();
        certificate!.Name.Should().Be("vigente-lejano");
    }

    [Fact]
    public async Task GetActiveByTaxIdAsync_WithoutACertificate_ReturnsNull()
    {
        await using var fixture = new CertificateFixture();
        await fixture.InitializeAsync();

        var certificate = await fixture.Repository.GetActiveByTaxIdAsync("131123456");

        certificate.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveByTaxIdAsync_WithOnlyAnExpiredCertificate_ReturnsNull()
    {
        await using var fixture = new CertificateFixture();
        await fixture.InitializeAsync();

        var certificate = await fixture.Repository.GetActiveByTaxIdAsync(ExpiredIssuerRnc);

        certificate.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveByTaxIdAsync_WithOnlyAnInactiveCertificate_ReturnsNull()
    {
        await using var fixture = new CertificateFixture();
        await fixture.InitializeAsync();

        var certificate = await fixture.Repository.GetActiveByTaxIdAsync(InactiveIssuerRnc);

        certificate.Should().BeNull();
    }

    private sealed class CertificateFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VolterpDbContext _context;

        public CertificateFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new VolterpDbContext(options);
        }

        public CompanyCertificateRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            var companyId = Guid.CreateVersion7();
            _context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Empresa",
                TaxId = IssuerRnc,
                Address = "Dirección",
                LegalName = "Empresa SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            });

            _context.CompanyCertificates.AddRange(
                Certificate(companyId, IssuerRnc, "vigente-cercano", DateTime.UtcNow.AddDays(10)),
                Certificate(companyId, IssuerRnc, "vigente-lejano", DateTime.UtcNow.AddDays(100)),
                Certificate(companyId, ExpiredIssuerRnc, "vencido", DateTime.UtcNow.AddDays(-1)),
                InactiveCertificate(companyId, InactiveIssuerRnc, "inactivo", DateTime.UtcNow.AddDays(50)));

            await _context.SaveChangesAsync();

            Repository = new CompanyCertificateRepository(_context);
        }

        private static CompanyCertificate Certificate(Guid companyId, string taxId, string name, DateTime expiresAt)
            => new()
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                Name = name,
                TaxId = taxId,
                ExpiresAt = expiresAt,
                Status = EntityStatus.Active,
                EncryptedCredential = "v1:ciphertext"
            };

        private static CompanyCertificate InactiveCertificate(Guid companyId, string taxId, string name, DateTime expiresAt)
        {
            var certificate = Certificate(companyId, taxId, name, expiresAt);
            certificate.Status = EntityStatus.Inactive;
            return certificate;
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
