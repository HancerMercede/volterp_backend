using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Volterp.Domain.Authorization;

namespace Volterp.Api.Authorization;

// The policies of the API: one per permission of the catalog.
//
// The endpoint declares the permission it needs with the name of its policy
// (Authorize(Policy = PermissionPolicies.SalesWrite)). The names are
// constants on purpose: a name that does not exist is a mistake the compiler
// sees, not a request that fails with a 500 in production.
public static class PermissionPolicies
{
    private const string Prefix = "perm.";

    public const string DashboardRead = "perm.dashboard.read";
    public const string DashboardWrite = "perm.dashboard.write";
    public const string DashboardDelete = "perm.dashboard.delete";

    public const string SalesRead = "perm.ventas.read";
    public const string SalesWrite = "perm.ventas.write";
    public const string SalesDelete = "perm.ventas.delete";

    public const string PurchasesRead = "perm.compras.read";
    public const string PurchasesWrite = "perm.compras.write";
    public const string PurchasesDelete = "perm.compras.delete";

    public const string InventoryRead = "perm.inventario.read";
    public const string InventoryWrite = "perm.inventario.write";
    public const string InventoryDelete = "perm.inventario.delete";

    public const string ClientsRead = "perm.clientes.read";
    public const string ClientsWrite = "perm.clientes.write";
    public const string ClientsDelete = "perm.clientes.delete";

    public const string SuppliersRead = "perm.proveedores.read";
    public const string SuppliersWrite = "perm.proveedores.write";
    public const string SuppliersDelete = "perm.proveedores.delete";

    public const string AccountingRead = "perm.contabilidad.read";
    public const string AccountingWrite = "perm.contabilidad.write";
    public const string AccountingDelete = "perm.contabilidad.delete";
    public const string AccountingPost = "perm.contabilidad.post";

    public const string EmployeesRead = "perm.rrhh.read";
    public const string EmployeesWrite = "perm.rrhh.write";
    public const string EmployeesDelete = "perm.rrhh.delete";

    public const string ProjectsRead = "perm.proyectos.read";
    public const string ProjectsWrite = "perm.proyectos.write";
    public const string ProjectsDelete = "perm.proyectos.delete";

    public const string ReportsRead = "perm.reportes.read";
    public const string ReportsWrite = "perm.reportes.write";
    public const string ReportsDelete = "perm.reportes.delete";

    public const string AdministrationRead = "perm.configuracion.read";
    public const string AdministrationWrite = "perm.configuracion.write";
    public const string AdministrationDelete = "perm.configuracion.delete";

    public const string ElectronicInvoicingRead = "perm.facturacion.read";
    public const string ElectronicInvoicingWrite = "perm.facturacion.write";
    public const string ElectronicInvoicingDelete = "perm.facturacion.delete";

    public const string PointOfSaleRead = "perm.pos.read";
    public const string PointOfSaleWrite = "perm.pos.write";
    public const string PointOfSaleVoid = "perm.pos.void";
    public const string PointOfSaleShiftOpen = "perm.pos.shift.open";
    public const string PointOfSaleShiftClose = "perm.pos.shift.close";
    public const string PointOfSaleShiftReopen = "perm.pos.shift.reopen";
    public const string PointOfSaleCashMovement = "perm.pos.cash.movement";
    public const string PointOfSaleSaleDiscount = "perm.pos.sale.discount";
    public const string PointOfSaleReportRead = "perm.pos.report.read";

    // The name of the policy of a permission: what the catalog says.
    public static string Name(Permission permission) => Prefix + permission.Key();

    // Every policy name that exists, read from the constants themselves.
    public static IReadOnlyList<string> Names { get; } = typeof(PermissionPolicies)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!)
        .ToList();

    // One policy per permission of the catalog: the endpoint declares the
    // permission it needs and the policy carries it.
    public static void Register(AuthorizationOptions options)
    {
        foreach (var permission in Permissions.All)
            options.AddPolicy(Name(permission), policy => policy.AddRequirements(new PermissionRequirement(permission)));
    }
}
