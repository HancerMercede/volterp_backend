using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Volterp.Domain.Abstractions;
using Volterp.Infrastructure.Data;
using Xunit;

namespace Volterp.Tests.Infrastructure;

internal class EntityWithoutTenantScopeDeclaration
{
    public int Id { get; set; }
    public Guid CompanyId { get; set; }
}

internal class EntityWithTenantScopeDeclaration : ITenantScoped
{
    public int Id { get; set; }
    public Guid CompanyId { get; set; }
}

public class TenantScopeGuardTests
{
    [Fact]
    public void EntityWithCompanyId_AndNoTenantScopedDeclaration_IsRejected()
    {
        var builder = new ModelBuilder();
        builder.Entity<EntityWithoutTenantScopeDeclaration>().HasKey(e => e.Id);

        var act = () => TenantScopeGuard.EnsureDeclared(builder.Model.GetEntityTypes());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*EntityWithoutTenantScopeDeclaration*");
    }

    [Fact]
    public void EntityWithTenantScopedDeclaration_IsAccepted()
    {
        var builder = new ModelBuilder();
        builder.Entity<EntityWithTenantScopeDeclaration>().HasKey(e => e.Id);

        var act = () => TenantScopeGuard.EnsureDeclared(builder.Model.GetEntityTypes());

        act.Should().NotThrow();
    }

    [Fact]
    public void EveryEntityWithCompanyId_HasATenantQueryFilter()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<VolterpDbContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new VolterpDbContext(options);
        var model = db.Model;

        var withCompanyId = model.GetEntityTypes()
            .Where(t => t.FindProperty(nameof(ITenantScoped.CompanyId)) is not null)
            .ToList();

        withCompanyId.Should().NotBeEmpty("the model carries tenant-scoped entities to assert on");
        foreach (var entityType in withCompanyId)
        {
            entityType.GetQueryFilter().Should().NotBeNull(
                $"{entityType.ClrType.Name} carries a CompanyId and must be filtered by tenant");
        }
    }
}
