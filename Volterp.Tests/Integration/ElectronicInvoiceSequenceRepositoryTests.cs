using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

public class ElectronicInvoiceSequenceRepositoryTests
{
    [Fact]
    public async Task NextSequenceAsync_WhenCalledTwice_ReturnsConsecutiveNumbers()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();

        var first = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, SaleDocumentType.FiscalCredit);
        var second = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, SaleDocumentType.FiscalCredit);

        first.Should().Be(1);
        second.Should().Be(2);
    }

    [Fact]
    public async Task NextSequenceAsync_WithDifferentDocumentTypes_KeepsSeparateSequences()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();

        var fiscalCredit = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, SaleDocumentType.FiscalCredit);
        var consumer = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, SaleDocumentType.Consumer);

        fiscalCredit.Should().Be(1);
        consumer.Should().Be(1);
    }

    [Fact]
    public async Task NextSequenceAsync_WithDifferentIssuers_KeepsSeparateSequences()
    {
        await using var fixture = new SequenceFixture();
        await fixture.InitializeAsync();

        var otherCompanyId = Guid.CreateVersion7();
        await fixture.AddCompanyAsync(otherCompanyId);

        var mine = await fixture.Repository.NextSequenceAsync(fixture.CompanyId, SaleDocumentType.FiscalCredit);
        var theirs = await fixture.Repository.NextSequenceAsync(otherCompanyId, SaleDocumentType.FiscalCredit);

        mine.Should().Be(1);
        theirs.Should().Be(1);
    }

    private sealed class SequenceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VolterpDbContext _context;

        public SequenceFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new VolterpDbContext(options);
        }

        public Guid CompanyId { get; private set; }
        public ElectronicInvoiceSequenceRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            await AddCompanyAsync(CompanyId);

            Repository = new ElectronicInvoiceSequenceRepository(_context);
        }

        public async Task AddCompanyAsync(Guid companyId)
        {
            _context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Empresa",
                TaxId = companyId.ToString("N")[^9..],
                Address = "Dirección",
                LegalName = "Empresa SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            });

            await _context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
