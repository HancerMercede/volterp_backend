using FluentAssertions;
using Volterp.Application.Services;
using Volterp.Domain.Authorization;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Services;

// The permissions of a caller: the union of the permissions of the roles it
// holds, resolved in the server (S6).
public class PermissionServiceTests
{
    private readonly PermissionService _sut = new();

    [Fact]
    public void ThePermissionsOfACallerAreTheUnionOfTheRoles()
    {
        var permissions = _sut.EffectivePermissions([UserRole.Vendedor, UserRole.Inventario]);

        permissions.Should().Contain("ventas.read");
        permissions.Should().Contain("inventario.read");
        permissions.Should().NotContain("contabilidad.post");
    }

    [Fact]
    public void ACallerWithoutRolesHasNoPermissions()
    {
        _sut.EffectivePermissions([]).Should().BeEmpty();
    }

    [Fact]
    public void ThePermissionsOfARoleAreTheOnesOfTheMatrix()
    {
        _sut.PermissionsOf(UserRole.Vendedor).Should().Contain("ventas.read").And.Contain("facturacion.read");

        _sut.PermissionsOf(UserRole.Vendedor).Should().NotContain("ventas.delete",
            "the matrix says what the API does: the sales role does not delete");
    }

    [Fact]
    public void TheAdminHasEveryPermissionOfTheCatalog()
    {
        _sut.PermissionsOf(UserRole.Admin).Should().HaveCount(
            Permissions.All.Count);
    }

    [Fact]
    public void TheMatrixCoversEveryRoleOfTheCatalog()
    {
        _sut.Matrix().Should().HaveCount(Enum.GetValues<UserRole>().Length);

        _sut.Matrix().Should().OnlyContain(entry => entry.Permissions.Length > 0,
            "a role that grants nothing is a role that cannot do anything");
    }
}
