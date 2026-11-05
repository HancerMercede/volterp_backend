using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

public class SaleRepositoryTests
{
    [Fact]
    public async Task GetSaleByIdAsync_LoadsTheCompanyOfTheSale()
    {
        await using var fixture = new SaleFixture();
        await fixture.InitializeAsync();

        var sale = await fixture.Repository.GetSaleByIdAsync(fixture.SaleId);

        sale.Should().NotBeNull();
        sale!.Company.Should().NotBeNull();
        sale.Company.Name.Should().Be("Empresa");
    }

    [Fact]
    public async Task GetSaleByIdAsync_KeepsLoadingTheItems()
    {
        await using var fixture = new SaleFixture();
        await fixture.InitializeAsync();

        var sale = await fixture.Repository.GetSaleByIdAsync(fixture.SaleId);

        sale.Should().NotBeNull();
        sale!.Items.Should().HaveCount(1);
        sale.Items.First().ProductName.Should().Be("Producto");
    }

    [Fact]
    public async Task GetSaleByIdAsync_WhenTheSaleDoesNotExist_ReturnsNull()
    {
        await using var fixture = new SaleFixture();
        await fixture.InitializeAsync();

        var sale = await fixture.Repository.GetSaleByIdAsync(Guid.CreateVersion7());

        sale.Should().BeNull();
    }

    private sealed class SaleFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VolterpDbContext _context;

        public SaleFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new VolterpDbContext(options);
        }

        public Guid SaleId { get; private set; }
        public SaleRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            var companyId = Guid.CreateVersion7();
            _context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Empresa",
                TaxId = "130862346",
                Address = "Dirección",
                LegalName = "Empresa SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            });

            var sale = new Sale
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                Status = SaleStatus.Pending
            };
            sale.AddItem(Guid.CreateVersion7(), "Producto", 2, 100m, TaxCode.ItbisTasa1);

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            SaleId = sale.Id;
            Repository = new SaleRepository(_context);
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
