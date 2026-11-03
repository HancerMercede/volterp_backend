using FluentAssertions;
using Volterp.Domain.Entities;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;
using Xunit;
using Volterp.Tests.TestHelpers;

namespace Volterp.Tests.Domain;

public class UserBehaviorTests
{
    // ===== ChangeRole =====

    [Fact]
    public void ChangeRole_FromVentasToAdmin_UpdatesRole()
    {
        var user = CreateUser(role: UserRole.Ventas);

        user.ChangeRole(UserRole.Admin);

        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void ChangeRole_FromAdminToVentas_UpdatesRole()
    {
        var user = CreateUser(role: UserRole.Admin);

        user.ChangeRole(UserRole.Ventas);

        user.Role.Should().Be(UserRole.Ventas);
    }

    [Fact]
    public void ChangeRole_FromAdminToContabilidad_UpdatesRole()
    {
        var user = CreateUser(role: UserRole.Admin);

        user.ChangeRole(UserRole.Contabilidad);

        user.Role.Should().Be(UserRole.Contabilidad);
    }

    [Fact]
    public void ChangeRole_FromVentasToInventario_UpdatesRole()
    {
        var user = CreateUser(role: UserRole.Ventas);

        user.ChangeRole(UserRole.Inventario);

        user.Role.Should().Be(UserRole.Inventario);
    }

    [Fact]
    public void ChangeRole_FromVentasToRRHH_UpdatesRole()
    {
        var user = CreateUser(role: UserRole.Ventas);

        user.ChangeRole(UserRole.RRHH);

        user.Role.Should().Be(UserRole.RRHH);
    }

    [Fact]
    public void ChangeRole_FromSuperAdminToLower_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser(role: UserRole.SuperAdmin);

        var act = () => user.ChangeRole(UserRole.Admin);

        act.Should().Throw<InvalidRoleTransitionException>()
            .Which.FromRole.Should().Be("SuperAdmin");
    }

    [Fact]
    public void ChangeRole_FromSuperAdminToVentas_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser(role: UserRole.SuperAdmin);

        var act = () => user.ChangeRole(UserRole.Ventas);

        act.Should().Throw<InvalidRoleTransitionException>();
    }

    [Fact]
    public void ChangeRole_FromAdminToSuperAdmin_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser(role: UserRole.Admin);

        var act = () => user.ChangeRole(UserRole.SuperAdmin);

        act.Should().Throw<InvalidRoleTransitionException>()
            .Which.ToRole.Should().Be("SuperAdmin");
    }

    [Fact]
    public void ChangeRole_FromVentasToSuperAdmin_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser(role: UserRole.Ventas);

        var act = () => user.ChangeRole(UserRole.SuperAdmin);

        act.Should().Throw<InvalidRoleTransitionException>();
    }

    [Fact]
    public void ChangeRole_FromSuperAdminToSuperAdmin_IsAllowed()
    {
        var user = CreateUser(role: UserRole.SuperAdmin);

        user.ChangeRole(UserRole.SuperAdmin);

        user.Role.Should().Be(UserRole.SuperAdmin);
    }

    [Fact]
    public void ChangeRole_WhenUserDeactivated_ThrowsInvalidRoleTransitionException()
    {
        var user = CreateUser(role: UserRole.Ventas);
        user.Deactivate();

        var act = () => user.ChangeRole(UserRole.Admin);

        act.Should().Throw<InvalidRoleTransitionException>()
            .Which.FromRole.Should().Be("Ventas");
    }

    // ===== Helpers =====

    private static User CreateUser(UserRole role = UserRole.Ventas)
    {
        return new User
        {
            Id = Guid7TestValues.UserA,
            Username = "testuser",
            Email = "test@example.com",
            FullName = "Test User",
            Role = role,
            IsActive = true
        };
    }
}
