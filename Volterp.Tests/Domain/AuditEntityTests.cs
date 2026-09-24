using FluentAssertions;
using Volterp.Domain.Entities;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Domain;

public class AuditEntityTests
{
    [Fact]
    public void AuditEntity_ShouldBeAbstract()
    {
        var type = typeof(AuditEntity);

        type.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void CreatedAt_ShouldDefaultToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var entity = new TestEntity();
        var after = DateTime.UtcNow.AddSeconds(1);

        entity.CreatedAt.Should().BeOnOrAfter(before);
        entity.CreatedAt.Should().BeOnOrBefore(after);
        entity.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void UpdatedAt_ShouldDefaultToNull()
    {
        var entity = new TestEntity();

        entity.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void CreatedBy_ShouldDefaultToNull()
    {
        var entity = new TestEntity();

        entity.CreatedBy.Should().BeNull();
    }

    [Fact]
    public void UpdatedBy_ShouldDefaultToNull()
    {
        var entity = new TestEntity();

        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void MarkUpdated_ShouldSetUpdatedAt()
    {
        var entity = new TestEntity();

        entity.MarkUpdated();

        entity.UpdatedAt.Should().NotBeNull();
        entity.UpdatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void MarkUpdated_ShouldSetUpdatedAtToRecent()
    {
        var entity = new TestEntity();
        var before = DateTime.UtcNow.AddSeconds(-1);

        entity.MarkUpdated();

        var after = DateTime.UtcNow.AddSeconds(1);
        entity.UpdatedAt!.Value.Should().BeOnOrAfter(before);
        entity.UpdatedAt!.Value.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void SetCreatedBy_ShouldSetCreatedBy()
    {
        var entity = new TestEntity();

        entity.SetCreatedBy(Guid7TestValues.UserA);

        entity.CreatedBy.Should().Be(Guid7TestValues.UserA);
    }

    [Fact]
    public void SetUpdatedBy_ShouldSetUpdatedBy()
    {
        var entity = new TestEntity();

        entity.SetUpdatedBy(Guid7TestValues.UserA);

        entity.UpdatedBy.Should().Be(Guid7TestValues.UserA);
    }
}

/// <summary>
/// Concrete subclass used to test the abstract AuditEntity.
/// No additional members — purely for instantiation.
/// </summary>
public class TestEntity : AuditEntity;
