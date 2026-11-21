using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using EitherWay;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Volterp.Api.Http;
using Volterp.Api.Configuration;
using Volterp.Api.Workers;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Xunit;

namespace Volterp.Tests.Integration;

// The cashier closes the sale: it stays on record as cancelled and the
// stock it reserved comes back.
[Collection("VolterpApiFactory")]
public class SalesControllerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SalesControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task TheCancelOfAPendingSaleAnswersTheCancelledSale()
    {
        var (saleId, productId) = await SeedAsync();

        var response = await _client.PutAsync($"/api/sales/{saleId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Cancelled");

        (await StockAsync(productId)).Should().Be(15,
            "the stock the cancelled sale reserved comes back");
    }

    [Fact]
    public async Task TheCancelOfASaleThatDoesNotExistIsNotFound()
    {
        SetAuth(Guid.CreateVersion7());

        var response = await _client.PutAsync($"/api/sales/{Guid.CreateVersion7()}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(Guid SaleId, Guid ProductId)> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var companyId = Guid.CreateVersion7();
        var saleId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Distribuidora del Norte",
            LegalName = "Distribuidora del Norte SRL",
            TaxId = RncOf(3),
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@distribuidora.do"
        });

        db.Products.Add(new Product
        {
            Id = productId,
            CompanyId = companyId,
            Code = $"P-{productId:N}"[..12],
            Name = "Café molido",
            Category = "Bebidas",
            Stock = 5,
            TaxCode = TaxCode.ItbisTasa1
        });

        var sale = new Sale
        {
            Id = saleId,
            CompanyId = companyId,
            Code = $"V-{Guid.NewGuid():N}"[..12],
            Status = SaleStatus.Pending,
            DocumentType = EcfDocumentType.Consumer,
            ClienteName = "Cliente de Prueba SRL",
            CreatedAt = DateTime.UtcNow
        };

        sale.AddItem(productId, "Café molido", 10, 100m, TaxCode.ItbisTasa1);
        db.Sales.Add(sale);

        await db.SaveChangesAsync();

        SetAuth(companyId);

        return (saleId, productId);
    }

    private async Task<int> StockAsync(Guid productId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var product = await db.Products
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == productId);

        return product.Stock;
    }

    private void SetAuth(Guid companyId)
    {
        var token = JwtTestHelper.MintToken(_factory, companyId, Guid.CreateVersion7(), "Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // The RNC of the company: the check digit is the one of the DGII, so the
    // company is a real taxpayer and not a name that happens to be free.
    private static string RncOf(int seed)
    {
        int[] weights = [7, 9, 8, 6, 5, 4, 3, 2];
        var digits = $"{seed:D8}";

        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
            sum += (digits[i] - '0') * weights[i];

        var remainder = sum % 11;
        var checkDigit = remainder switch
        {
            0 => 2,
            1 => 1,
            _ => 11 - remainder
        };

        return digits + checkDigit;
    }
}

// The worker of the pending sales: it calls the sweep with the lifetime
// that the settings carry.
public class PendingSalesExpiryWorkerTests
{
    [Fact]
    public async Task TheWorkerCallsTheSweepWithTheLifetimeOfTheSettings()
    {
        var sales = new Mock<ISaleService>();
        sales.Setup(s => s.ExpirePendingSalesAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Either<Error, int>.ToRight(3));

        var services = new Mock<IServiceManager>();
        services.SetupGet(manager => manager.Sales).Returns(sales.Object);

        using var provider = Provider(services.Object);

        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Production");

        var worker = new PendingSalesExpiryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new PendingSalesOptions { LifetimeHours = 6, SweepIntervalMinutes = 30 },
            TimeProvider.System,
            environment.Object,
            NullLogger<PendingSalesExpiryWorker>.Instance);

        await worker.ExpireAsync(CancellationToken.None);

        sales.Verify(s => s.ExpirePendingSalesAsync(
            TimeSpan.FromHours(6), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ServiceProvider Provider(IServiceManager services)
    {
        var collection = new ServiceCollection();
        collection.AddScoped(_ => services);

        return collection.BuildServiceProvider();
    }
}

// The sweep of the pending sales. It runs without an HTTP request, so the
// filter of the tenant is on: the queries of the sweep have to carry the
// company by hand, or the sale and the product it reserved would be
// invisible.
public class PendingSalesExpiryTests
{
    [Fact]
    public async Task TheSweepClosesThePendingSaleNobodyDecidedAndBringsTheStockBack()
    {
        await using var fixture = new ExpiryFixture();
        await fixture.InitializeAsync();

        var closed = await fixture.ExpireAsync();

        closed.Should().Be(1);
        (await fixture.StatusOfAsync(fixture.ExpiredSaleId)).Should().Be(SaleStatus.Cancelled);
        (await fixture.StockAsync()).Should().Be(15,
            "the stock the expired sale reserved comes back");
        (await fixture.StatusOfAsync(fixture.RecentSaleId)).Should().Be(SaleStatus.Pending,
            "the customer still has time to decide");
    }

    private sealed class ExpiryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<VolterpDbContext> _options;

        public ExpiryFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<VolterpDbContext>().UseSqlite(_connection).Options;
        }

        public Guid ExpiredSaleId { get; private set; }
        public Guid RecentSaleId { get; private set; }
        public Guid ProductId { get; private set; }

        public async Task InitializeAsync()
        {
            await using var context = new VolterpDbContext(_options);
            await context.Database.EnsureCreatedAsync();

            var companyId = Guid.CreateVersion7();
            ProductId = Guid.CreateVersion7();
            ExpiredSaleId = Guid.CreateVersion7();
            RecentSaleId = Guid.CreateVersion7();

            context.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Distribuidora del Norte",
                LegalName = "Distribuidora del Norte SRL",
                TaxId = "10100000101",
                Address = "Calle Principal 1, Santo Domingo",
                Phone = "809-555-1234",
                Email = "facturacion@distribuidora.do"
            });

            context.Products.Add(new Product
            {
                Id = ProductId,
                CompanyId = companyId,
                Code = "P-001",
                Name = "Café molido",
                Category = "Bebidas",
                Stock = 5,
                TaxCode = TaxCode.ItbisTasa1
            });

            context.Sales.Add(SaleOf(companyId, ExpiredSaleId, DateTime.UtcNow.AddHours(-30)));
            context.Sales.Add(SaleOf(companyId, RecentSaleId, DateTime.UtcNow.AddHours(-2)));

            await context.SaveChangesAsync();
        }

        // The sweep of the worker: the container gives it the tenant of the
        // request, and there is no request.
        public async Task<int> ExpireAsync()
        {
            await using var context = new VolterpDbContext(_options, new HttpTenantContext(new HttpContextAccessor()));

            var unitOfWork = new UnitOfWork(context);
            var service = new SaleService(
                unitOfWork,
                new Mock<IElectronicInvoiceService>().Object,
                new Mock<INormalFiscalDocumentService>().Object);

            var result = await service.ExpirePendingSalesAsync(TimeSpan.FromHours(24));

            result.Should().BeOfType<Either<Error, int>.Right>();

            return ((Either<Error, int>.Right)result).Value;
        }

        public async Task<SaleStatus> StatusOfAsync(Guid saleId)
        {
            await using var context = new VolterpDbContext(_options);
            context.ChangeTracker.Clear();

            var sale = await context.Sales
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == saleId);

            return sale.Status;
        }

        public async Task<int> StockAsync()
        {
            await using var context = new VolterpDbContext(_options);

            var product = await context.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == ProductId);

            return product.Stock;
        }

        private Sale SaleOf(Guid companyId, Guid saleId, DateTime createdAt)
        {
            var sale = new Sale
            {
                Id = saleId,
                CompanyId = companyId,
                Code = $"V-{saleId:N}",
                Status = SaleStatus.Pending,
                DocumentType = EcfDocumentType.Consumer,
                CreatedAt = createdAt
            };

            sale.AddItem(ProductId, "Café molido", 10, 100m, TaxCode.ItbisTasa1);
            return sale;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
