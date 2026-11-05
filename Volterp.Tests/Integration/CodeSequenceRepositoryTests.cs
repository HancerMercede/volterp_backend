using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

public class CodeSequenceRepositoryTests
{
    [Fact]
    public async Task NextValueAsync_WhenCalledTwice_ReturnsConsecutiveNumbers()
    {
        await using var fixture = new CodeSequenceFixture();
        await fixture.InitializeAsync();

        var first = await fixture.Repository.NextValueAsync(fixture.CompanyId, CodeEntityType.Product);
        var second = await fixture.Repository.NextValueAsync(fixture.CompanyId, CodeEntityType.Product);

        first.Should().Be(1);
        second.Should().Be(2);
    }

    [Fact]
    public async Task NextValueAsync_WithDifferentEntityTypes_KeepsSeparateSequences()
    {
        await using var fixture = new CodeSequenceFixture();
        await fixture.InitializeAsync();

        var product = await fixture.Repository.NextValueAsync(fixture.CompanyId, CodeEntityType.Product);
        var client = await fixture.Repository.NextValueAsync(fixture.CompanyId, CodeEntityType.Client);

        product.Should().Be(1);
        client.Should().Be(1);
    }

    [Fact]
    public async Task NextValueAsync_WithDifferentCompanies_KeepsSeparateSequences()
    {
        await using var fixture = new CodeSequenceFixture();
        await fixture.InitializeAsync();

        var otherCompanyId = Guid.CreateVersion7();
        await fixture.AddCompanyAsync(otherCompanyId);

        var mine = await fixture.Repository.NextValueAsync(fixture.CompanyId, CodeEntityType.Product);
        var theirs = await fixture.Repository.NextValueAsync(otherCompanyId, CodeEntityType.Product);

        mine.Should().Be(1);
        theirs.Should().Be(1);
    }

    [Fact]
    public async Task NextValueAsync_WhenTransactionRollsBack_DoesNotBurnTheNumber()
    {
        await using var fixture = new CodeSequenceFixture();
        await fixture.InitializeAsync();

        await using (var transaction = await fixture.Context.Database.BeginTransactionAsync())
        {
            await fixture.Repository.NextValueAsync(fixture.CompanyId, CodeEntityType.Product);
            await transaction.RollbackAsync();
        }

        var afterRollback = await fixture.Repository.NextValueAsync(fixture.CompanyId, CodeEntityType.Product);

        afterRollback.Should().Be(1);
    }

    private sealed class CodeSequenceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VolterpDbContext _context;

        public CodeSequenceFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new VolterpDbContext(options);
        }

        public Guid CompanyId { get; private set; }
        public VolterpDbContext Context => _context;
        public CodeSequenceRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            await AddCompanyAsync(CompanyId);

            Repository = new CodeSequenceRepository(_context);
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
