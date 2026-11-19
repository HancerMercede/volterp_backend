using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;
using Xunit;

namespace Volterp.Tests.Integration;

// The issuer RNCs of a tenant: the RNC of the company itself and the ones
// of its branches. The sale is issued by one of them and by no other.
public class CompanyIssuerRepositoryTests
{
    private const string CompanyRnc = "101500018";
    private const string BranchRnc = "101500026";
    private const string InactiveBranchRnc = "101500034";
    private const string ForeignRnc = "131123432";

    [Fact]
    public async Task IssuerTaxIdsAsync_ReturnsTheRncOfTheCompanyAndItsActiveBranches()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var issuers = await fixture.Repository.IssuerTaxIdsAsync(fixture.CompanyId);

        issuers.Should().BeEquivalentTo([CompanyRnc, BranchRnc]);
    }

    [Fact]
    public async Task IssuerTaxIdsAsync_WithoutABranch_ReturnsTheRncOfTheCompany()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var issuers = await fixture.Repository.IssuerTaxIdsAsync(Guid.NewGuid());

        issuers.Should().BeEmpty("a company that does not exist has no issuer");
    }

    [Fact]
    public async Task ResolveIssuerTaxIdAsync_WithoutARequest_ReturnsTheRncOfTheCompany()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var issuer = await fixture.Repository.ResolveIssuerTaxIdAsync(fixture.CompanyId, null);

        issuer.Should().Be(CompanyRnc);
    }

    [Fact]
    public async Task ResolveIssuerTaxIdAsync_WithABranch_ReturnsItsRnc()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var issuer = await fixture.Repository.ResolveIssuerTaxIdAsync(fixture.CompanyId, BranchRnc);

        issuer.Should().Be(BranchRnc);
    }

    [Fact]
    public async Task ResolveIssuerTaxIdAsync_WithABranchThatDoesNotIssue_Fails()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var act = () => fixture.Repository.ResolveIssuerTaxIdAsync(fixture.CompanyId, InactiveBranchRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResolveIssuerTaxIdAsync_WithAnRncOfAnotherTenant_Fails()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var act = () => fixture.Repository.ResolveIssuerTaxIdAsync(fixture.CompanyId, ForeignRnc);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a tenant cannot issue with the RNC of another one");
    }

    [Fact]
    public async Task CompanyOfIssuerAsync_WithTheRncOfABranch_FindsTheTenant()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var company = await fixture.Repository.CompanyOfIssuerAsync(BranchRnc);

        company.Should().NotBeNull();
        company!.Id.Should().Be(fixture.CompanyId);
    }

    [Fact]
    public async Task CompanyOfIssuerAsync_WithAnUnknownRnc_ReturnsNull()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        var company = await fixture.Repository.CompanyOfIssuerAsync("999999999");

        company.Should().BeNull();
    }

    [Fact]
    public async Task CompanyOfIssuerAsync_WithAnRncOfTwoTenants_Fails()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        // The RNC of the company registered as a branch of another tenant:
        // the receiver is ambiguous and the document cannot be attributed
        // to one of them.
        fixture.Context.CompanyIssuers.Add(new CompanyIssuer
        {
            Id = Guid.CreateVersion7(),
            CompanyId = fixture.OtherCompanyId,
            TaxId = CompanyRnc,
            LegalName = "Sucursal de otro tenant"
        });

        await fixture.Context.SaveChangesAsync();

        var act = () => fixture.Repository.CompanyOfIssuerAsync(CompanyRnc);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TheSameRncCannotBeRegisteredByTwoTenants()
    {
        await using var fixture = new IssuerFixture();
        await fixture.InitializeAsync();

        // The RNC of a branch of this tenant, registered by another one:
        // the database refuses it, because an RNC is of one taxpayer.
        fixture.Context.CompanyIssuers.Add(new CompanyIssuer
        {
            Id = Guid.CreateVersion7(),
            CompanyId = fixture.OtherCompanyId,
            TaxId = BranchRnc,
            LegalName = "Sucursal de otro tenant"
        });

        var act = () => fixture.Context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "the index of the RNC is unique in the country: the second tenant that "
            + "registers it does not get it");
    }

    private sealed class IssuerFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public IssuerFixture()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<VolterpDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new VolterpDbContext(options);
        }

        public VolterpDbContext Context { get; }

        public Guid CompanyId { get; private set; } = Guid.CreateVersion7();

        public Guid OtherCompanyId { get; private set; } = Guid.CreateVersion7();

        public CompanyIssuerRepository Repository { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await Context.Database.EnsureCreatedAsync();

            Context.Companies.Add(new Company
            {
                Id = CompanyId,
                Name = "Empresa",
                TaxId = CompanyRnc,
                Address = "Dirección",
                LegalName = "Empresa SRL",
                Phone = "8090000000",
                Email = "empresa@test.com"
            });

            Context.Companies.Add(new Company
            {
                Id = OtherCompanyId,
                Name = "Otra",
                TaxId = "101500042",
                Address = "Dirección",
                LegalName = "Otra SRL",
                Phone = "8090000001",
                Email = "otra@test.com"
            });

            Context.CompanyIssuers.AddRange(
                Branch(CompanyId, BranchRnc, "Sucursal Norte", EntityStatus.Active),
                Branch(CompanyId, InactiveBranchRnc, "Sucursal Sur", EntityStatus.Inactive));

            await Context.SaveChangesAsync();

            Repository = new CompanyIssuerRepository(Context);
        }

        private static CompanyIssuer Branch(Guid companyId, string taxId, string legalName, EntityStatus status)
            => new()
            {
                Id = Guid.CreateVersion7(),
                CompanyId = companyId,
                TaxId = taxId,
                LegalName = legalName,
                Status = status
            };

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
