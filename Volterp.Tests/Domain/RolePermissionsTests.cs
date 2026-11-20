using FluentAssertions;
using Volterp.Domain.Authorization;
using Volterp.Domain.Enums;
using Xunit;

namespace Volterp.Tests.Domain;

// The matrix that lives in the API: the one the front has been managing
// moves here, and the front stops keeping its own copy (S6).
public class RolePermissionsTests
{
    [Fact]
    public void EveryPermissionOfTheCatalogHasItsOwnKey()
    {
        var keys = Permissions.All.Select(permission => permission.Key()).ToList();

        keys.Should().OnlyContain(key => !string.IsNullOrWhiteSpace(key));
        keys.Should().HaveCount(keys.Distinct().Count(),
            "two permissions with the same key are one permission");
    }

    [Fact]
    public void TheKeyOfAPermissionStartsWithTheKeyOfItsModule()
    {
        foreach (var permission in Permissions.All)
        {
            var module = permission.ModuleOf();

            permission.Key().Should().StartWith(module.ModuleKey(),
                "the key of a permission is the module it belongs to and its action");
        }
    }

    [Fact]
    public void TheKeysAreTheOnesTheFrontAlreadyUses()
    {
        Permission.DashboardRead.Key().Should().Be("dashboard.read");
        Permission.SalesRead.Key().Should().Be("ventas.read");
        Permission.PurchasesWrite.Key().Should().Be("compras.write");
        Permission.InventoryRead.Key().Should().Be("inventario.read");
        Permission.ClientsRead.Key().Should().Be("clientes.read");
        Permission.SuppliersRead.Key().Should().Be("proveedores.read");
        Permission.AccountingPost.Key().Should().Be("contabilidad.post");
        Permission.EmployeesRead.Key().Should().Be("rrhh.read");
        Permission.ProjectsRead.Key().Should().Be("proyectos.read");
        Permission.ReportsRead.Key().Should().Be("reportes.read");
        Permission.AdministrationRead.Key().Should().Be("configuracion.read");
        Permission.ElectronicInvoicingRead.Key().Should().Be("facturacion.read");
        Permission.PointOfSaleShiftOpen.Key().Should().Be("pos.shift.open");
        Permission.PointOfSaleReportRead.Key().Should().Be("pos.report.read");
    }

    [Fact]
    public void TheAdminGrantsEveryPermissionOfTheCatalog()
    {
        RolePermissions.For(UserRole.Admin).Should().BeEquivalentTo(Permissions.All);
        RolePermissions.For(UserRole.SuperAdmin).Should().BeEquivalentTo(Permissions.All);
    }

    [Fact]
    public void EveryPermissionIsGrantedToSomeRole()
    {
        var granted = RolePermissions.Matrix.Values.SelectMany(permissions => permissions).ToHashSet();

        Permissions.All.Should().BeEquivalentTo(granted,
            "a permission that nobody has is a door that nobody can open");
    }

    [Fact]
    public void EveryRoleOfTheCatalogHasARowInTheMatrix()
    {
        RolePermissions.Matrix.Keys.Should().BeEquivalentTo(Enum.GetValues<UserRole>());
    }

    [Fact]
    public void TheRoleOfVentasGrantsWhatTheApiDoes()
    {
        // The matrix says what the API does. The front used to offer the sales
        // role the writing of the clients and of the fiscal configuration, and
        // the API refused it with a 403: the buttons were a lie. The matrix
        // now says the truth, and the front stops offering them (T8).
        Granted(UserRole.Vendedor).Should().BeEquivalentTo(new[]
        {
            "dashboard.read",
            "dashboard.write",
            "ventas.read",
            "ventas.write",
            "clientes.read",
            "proveedores.read",
            "reportes.read",
            "facturacion.read"
        });
    }

    [Fact]
    public void TheRoleOfInventarioGrantsWhatTheFrontGranted()
    {
        Granted(UserRole.Inventario).Should().BeEquivalentTo(new[]
        {
            "dashboard.read",
            "dashboard.write",
            "compras.read",
            "compras.write",
            "inventario.read",
            "inventario.write",
            "proveedores.read",
            "proveedores.write",
            "reportes.read"
        });
    }

    [Fact]
    public void TheRoleOfContabilidadGrantsWhatTheFrontGranted()
    {
        Granted(UserRole.Contador).Should().BeEquivalentTo(new[]
        {
            "dashboard.read",
            "dashboard.write",
            "contabilidad.read",
            "contabilidad.write",
            "reportes.read"
        });
    }

    [Fact]
    public void TheRoleOfRrhhGrantsWhatTheFrontGranted()
    {
        Granted(UserRole.RRHH).Should().BeEquivalentTo(new[]
        {
            "dashboard.read",
            "dashboard.write",
            "rrhh.read",
            "rrhh.write",
            "reportes.read"
        });
    }

    [Fact]
    public void TheRolesThatAreNotAdminDoNotDelete()
    {
        // The deletion of a document is the admin's: the front used to give
        // the delete of the invoicing to the role of sales, and the API never
        // let it happen. Now the matrix says the same as the API.
        Granted(UserRole.Vendedor).Where(key => key.EndsWith(".delete"))
            .Should().BeEmpty();

        Granted(UserRole.Inventario).Where(key => key.EndsWith(".delete"))
            .Should().BeEmpty();
        Granted(UserRole.Contador).Where(key => key.EndsWith(".delete"))
            .Should().BeEmpty();
        Granted(UserRole.RRHH).Where(key => key.EndsWith(".delete"))
            .Should().BeEmpty();
    }

    private static string[] Granted(UserRole role)
        => RolePermissions.For(role).Select(permission => permission.Key()).ToArray();
}
