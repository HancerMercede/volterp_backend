using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EitherWay;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Volterp.Application.DTOs.SaleDtos;
using Volterp.Application.Exceptions.AppErrors;
using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.ElectronicInvoicing;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.UnitOfWork;
using Volterp.Tests.TestHelpers;
using Xunit;

namespace Volterp.Tests.Integration;

// The commercial invoice: the sale of the normal path that leaves without a
// fiscal document. It consumes no number, reaches no one and leaves no
// trace, and the electronic path does not offer it at all.
public class CompleteSaleCommercialInvoiceTests
{
    [Fact]
    public async Task TheCommercialInvoice_LeavesNoDocumentAndTouchesNoRange()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();

        var result = await fixture.CompleteSaleAsync(asCommercialInvoice: true);

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);
        (await fixture.ReadAsync(context => context.ElectronicInvoices.CountAsync())).Should().Be(0);

        // The number lives in the range: a sale that takes no number leaves
        // the range where it was.
        var normal = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        normal.CurrentSequence.Should().Be(0);

        var sale = await fixture.ReadAsync(context => context.Sales
            .AsNoTracking()
            .SingleAsync(s => s.Id == fixture.SaleId));
        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public async Task TheCommercialInvoice_ReachesNobody()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();

        await fixture.CompleteSaleAsync(asCommercialInvoice: true);

        fixture.ElectronicIssuance.Verify(e => e.IssueAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WithoutTheFlag_TheSaleStillTakesTheFiscalDocument()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync();

        var result = await fixture.CompleteSaleAsync();

        result.Should().BeOfType<Either<Error, SaleDto>.Right>();

        var document = await fixture.ReadAsync(context => context.NormalFiscalDocuments
            .AsNoTracking()
            .SingleAsync());
        document.Number.Should().Be("B0200000001");

        var normal = await fixture.ReadAsync(context => context.NormalFiscalSequences
            .AsNoTracking()
            .SingleAsync());
        normal.CurrentSequence.Should().Be(1);
    }

    [Fact]
    public async Task OnTheElectronicPath_TheCommercialInvoiceIsRefusedAndTheSaleDoesNotComplete()
    {
        await using var fixture = new Fixture();
        await fixture.InitializeAsync(emissionMode: CompanyEmissionMode.Electronic);

        var result = await fixture.CompleteSaleAsync(asCommercialInvoice: true);

        result.Should().BeOfType<Either<Error, SaleDto>.Left>();
        ((Either<Error, SaleDto>.Left)result).Value.Message.Should().Contain("normal mode");

        var sale = await fixture.ReadAsync(context => context.Sales
            .AsNoTracking()
            .SingleAsync(s => s.Id == fixture.SaleId));
        sale.Status.Should().Be(SaleStatus.Pending);

        (await fixture.ReadAsync(context => context.NormalFiscalDocuments.CountAsync())).Should().Be(0);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private const string IssuerRnc = "10100000101";

        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<VolterpDbContext> _options;

        public Fixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<VolterpDbContext>().UseSqlite(_connection).Options;
            Context = new VolterpDbContext(_options);
        }

        public VolterpDbContext Context { get; }
        public UnitOfWork UnitOfWork { get; private set; } = null!;
        public SaleService Sales { get; private set; } = null!;
        public Mock<IElectronicInvoiceService> ElectronicIssuance { get; } = new();
        public Guid CompanyId { get; private set; }
        public Guid SaleId { get; private set; }

        public async Task InitializeAsync(CompanyEmissionMode emissionMode = CompanyEmissionMode.Normal)
        {
            await Context.Database.EnsureCreatedAsync();

            CompanyId = Guid.CreateVersion7();
            SaleId = Guid.CreateVersion7();

            Context.Companies.Add(new Company
            {
                Id = CompanyId,
                Name = "Distribuidora del Norte",
                LegalName = "Distribuidora del Norte SRL",
                TaxId = IssuerRnc,
                Address = "Calle Principal 1, Santo Domingo",
                Phone = "809-555-1234",
                Email = "facturacion@distribuidora.do",
                EmissionMode = emissionMode
            });

            Context.NormalFiscalSequences.Add(new NormalFiscalSequence
            {
                Id = Guid.CreateVersion7(),
                CompanyId = CompanyId,
                IssuerTaxId = IssuerRnc,
                DocumentType = NormalFiscalDocumentType.Consumer,
                From = 1,
                To = 50_000_000,
                CurrentSequence = 0
            });

            var sale = new Sale
            {
                Id = SaleId,
                CompanyId = CompanyId,
                Code = "V-0001",
                Status = SaleStatus.Pending,
                DocumentType = EcfDocumentType.Consumer,
                ReceptorTaxId = "101010101",
                ClienteName = "Cliente de Prueba SRL",
                CreatedAt = new DateTime(2026, 11, 16)
            };

            sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);
            Context.Sales.Add(sale);

            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();

            UnitOfWork = new UnitOfWork(Context);
            Sales = new SaleService(
                UnitOfWork,
                ElectronicIssuance.Object,
                normalDocuments: new NormalFiscalDocumentService(UnitOfWork));
        }

        public Task<Either<Error, SaleDto>> CompleteSaleAsync(bool asCommercialInvoice = false)
            => Sales.CompleteSaleAsync(SaleId, CompanyId, Guid.CreateVersion7(), asCommercialInvoice);

        public async Task<T> ReadAsync<T>(Func<VolterpDbContext, Task<T>> read)
        {
            await using var context = new VolterpDbContext(_options);
            return await read(context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

// The wire: the flag arrives in the body of the completion, and a body that
// does not arrive keeps the behavior of today.
[Collection("VolterpApiFactory")]
public class CompleteSaleCommercialInvoiceEndpointTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CompleteSaleCommercialInvoiceEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.EnsureSchemaCreated();
    }

    [Fact]
    public async Task TheBodyAsksForTheCommercialInvoice()
    {
        var (companyId, saleId) = await SeedAsync(1);

        var response = await _client.PutAsJsonAsync(
            $"/api/Sales/{saleId}/complete",
            new { asCommercialInvoice = true });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DocumentsAsync(companyId)).Should().Be(0);
    }

    [Fact]
    public async Task WithoutABody_TheSaleKeepsTheFiscalDocument()
    {
        var (companyId, saleId) = await SeedAsync(2);

        var response = await _client.PutAsync($"/api/Sales/{saleId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DocumentsAsync(companyId)).Should().Be(1);
    }

    private async Task<(Guid CompanyId, Guid SaleId)> SeedAsync(int seed)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        var companyId = Guid.CreateVersion7();
        var saleId = Guid.CreateVersion7();
        var taxId = RncOf(seed);

        db.Set<Company>().Add(new Company
        {
            Id = companyId,
            Name = "Distribuidora del Norte",
            LegalName = "Distribuidora del Norte SRL",
            TaxId = taxId,
            Address = "Calle Principal 1, Santo Domingo",
            Phone = "809-555-1234",
            Email = "facturacion@distribuidora.do",
            EmissionMode = CompanyEmissionMode.Normal
        });

        db.Set<NormalFiscalSequence>().Add(new NormalFiscalSequence
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            IssuerTaxId = taxId,
            DocumentType = NormalFiscalDocumentType.Consumer,
            From = 1,
            To = 50_000_000,
            CurrentSequence = 0
        });

        var sale = new Sale
        {
            Id = saleId,
            CompanyId = companyId,
            Code = $"V-{Guid.NewGuid():N}"[..12],
            Status = SaleStatus.Pending,
            DocumentType = EcfDocumentType.Consumer,
            ReceptorTaxId = "101010101",
            ClienteName = "Cliente de Prueba SRL",
            CreatedAt = new DateTime(2026, 11, 16)
        };

        sale.AddItem(Guid.CreateVersion7(), "Café molido", 2, 100m, TaxCode.ItbisTasa1);
        db.Set<Sale>().Add(sale);

        await db.SaveChangesAsync();

        var token = JwtTestHelper.MintToken(_factory, companyId, Guid.CreateVersion7(), "Admin");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (companyId, saleId);
    }

    private async Task<int> DocumentsAsync(Guid companyId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VolterpDbContext>();

        return await db.Set<NormalFiscalDocument>()
            .IgnoreQueryFilters()
            .CountAsync(d => d.CompanyId == companyId);
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
