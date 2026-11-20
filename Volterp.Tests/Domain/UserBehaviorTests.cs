using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Domain;

public class UserBehaviorTests
{
    // ===== AssignRole =====

    [Fact]
    public void AssignRole_AddsTheRole()
    {
        var user = CreateUser();

        user.AssignRole(UserRole.Ventas);

        user.Roles.Select(assignment => assignment.Role)
            .Should().ContainSingle().Which.Should().Be(UserRole.Ventas);
    }

    [Fact]
    public void AssignRole_Twice_KeepsOneAssignmentOfTheRole()
    {
        var user = CreateUser();

        user.AssignRole(UserRole.Ventas);
        user.AssignRole(UserRole.Ventas);

        user.Roles.Should().ContainSingle();
    }

    [Fact]
    public void AssignRole_KeepsTheRolesTheUserAlreadyHolds()
    {
        var user = CreateUser();
        user.AssignRole(UserRole.Ventas);

        user.AssignRole(UserRole.Inventario);

        user.Roles.Select(assignment => assignment.Role)
            .Should().BeEquivalentTo([UserRole.Ventas, UserRole.Inventario]);
    }

    [Fact]
    public void AssignRole_CarriesTheCompanyOfTheUser()
    {
        var user = CreateUser();

        user.AssignRole(UserRole.Ventas);

        user.Roles.Should().ContainSingle().Which.CompanyId.Should().Be(user.CompanyId,
            "the assignment carries the company of the user: the tenant filter reads it");
    }

    [Fact]
    public void AssignRole_WhenUserDeactivated_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser();
        user.Deactivate();

        var act = () => user.AssignRole(UserRole.Admin);

        act.Should().Throw<InvalidRoleTransitionException>()
            .Which.FromRole.Should().Be(User.NoRole);
    }

    // ===== RemoveRole =====

    [Fact]
    public void RemoveRole_TakesTheRoleAway()
    {
        var user = CreateUser();
        user.AssignRole(UserRole.Ventas);
        user.AssignRole(UserRole.Inventario);

        user.RemoveRole(UserRole.Ventas);

        user.Roles.Select(assignment => assignment.Role)
            .Should().ContainSingle().Which.Should().Be(UserRole.Inventario);
    }

    [Fact]
    public void RemoveRole_WhenTheRoleIsNotHeld_DoesNothing()
    {
        var user = CreateUser();
        user.AssignRole(UserRole.Ventas);

        user.RemoveRole(UserRole.RRHH);

        user.Roles.Should().ContainSingle();
    }

    [Fact]
    public void RemoveRole_WhenItIsTheLastRole_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser();
        user.AssignRole(UserRole.Ventas);

        var act = () => user.RemoveRole(UserRole.Ventas);

        act.Should().Throw<InvalidRoleTransitionException>()
            .Which.ToRole.Should().Be(User.NoRole,
                "a user without roles has no permissions and would be locked out");
    }

    [Fact]
    public void RemoveRole_WhenUserDeactivated_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser();
        user.AssignRole(UserRole.Ventas);
        user.Deactivate();

        var act = () => user.RemoveRole(UserRole.Ventas);

        act.Should().Throw<InvalidRoleTransitionException>();
    }

    // ===== HasRole =====

    [Fact]
    public void HasRole_TellsWhetherTheUserHoldsTheRole()
    {
        var user = CreateUser();
        user.AssignRole(UserRole.Ventas);

        user.HasRole(UserRole.Ventas).Should().BeTrue();
        user.HasRole(UserRole.Admin).Should().BeFalse();
    }

    // ===== Helpers =====

    private static User CreateUser()
    {
        return new User
        {
            Id = Guid7TestValues.UserA,
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            CompanyId = Guid7TestValues.CompanyA,
            IsActive = true
        };
    }
}
