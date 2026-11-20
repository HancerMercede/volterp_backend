namespace Volterp.Domain.Authorization;

// What every permission of the catalog is: the module it belongs to and the
// key that travels to the front.
//
// The switches have no default arm on purpose: a permission that is added to
// the enum and forgotten here does not compile.
public static class Permissions
{
    public static (Module Module, string Key) Describe(this Permission permission) => permission switch
    {
        Permission.DashboardRead => (Module.Dashboard, "dashboard.read"),
        Permission.DashboardWrite => (Module.Dashboard, "dashboard.write"),
        Permission.DashboardDelete => (Module.Dashboard, "dashboard.delete"),

        Permission.SalesRead => (Module.Sales, "ventas.read"),
        Permission.SalesWrite => (Module.Sales, "ventas.write"),
        Permission.SalesDelete => (Module.Sales, "ventas.delete"),

        Permission.PurchasesRead => (Module.Purchases, "compras.read"),
        Permission.PurchasesWrite => (Module.Purchases, "compras.write"),
        Permission.PurchasesDelete => (Module.Purchases, "compras.delete"),

        Permission.InventoryRead => (Module.Inventory, "inventario.read"),
        Permission.InventoryWrite => (Module.Inventory, "inventario.write"),
        Permission.InventoryDelete => (Module.Inventory, "inventario.delete"),

        Permission.ClientsRead => (Module.Clients, "clientes.read"),
        Permission.ClientsWrite => (Module.Clients, "clientes.write"),
        Permission.ClientsDelete => (Module.Clients, "clientes.delete"),

        Permission.SuppliersRead => (Module.Suppliers, "proveedores.read"),
        Permission.SuppliersWrite => (Module.Suppliers, "proveedores.write"),
        Permission.SuppliersDelete => (Module.Suppliers, "proveedores.delete"),

        Permission.AccountingRead => (Module.Accounting, "contabilidad.read"),
        Permission.AccountingWrite => (Module.Accounting, "contabilidad.write"),
        Permission.AccountingDelete => (Module.Accounting, "contabilidad.delete"),
        Permission.AccountingPost => (Module.Accounting, "contabilidad.post"),

        Permission.EmployeesRead => (Module.Employees, "rrhh.read"),
        Permission.EmployeesWrite => (Module.Employees, "rrhh.write"),
        Permission.EmployeesDelete => (Module.Employees, "rrhh.delete"),

        Permission.ProjectsRead => (Module.Projects, "proyectos.read"),
        Permission.ProjectsWrite => (Module.Projects, "proyectos.write"),
        Permission.ProjectsDelete => (Module.Projects, "proyectos.delete"),

        Permission.ReportsRead => (Module.Reports, "reportes.read"),
        Permission.ReportsWrite => (Module.Reports, "reportes.write"),
        Permission.ReportsDelete => (Module.Reports, "reportes.delete"),

        Permission.AdministrationRead => (Module.Administration, "configuracion.read"),
        Permission.AdministrationWrite => (Module.Administration, "configuracion.write"),
        Permission.AdministrationDelete => (Module.Administration, "configuracion.delete"),

        Permission.ElectronicInvoicingRead => (Module.ElectronicInvoicing, "facturacion.read"),
        Permission.ElectronicInvoicingWrite => (Module.ElectronicInvoicing, "facturacion.write"),
        Permission.ElectronicInvoicingDelete => (Module.ElectronicInvoicing, "facturacion.delete"),

        Permission.PointOfSaleRead => (Module.PointOfSale, "pos.read"),
        Permission.PointOfSaleWrite => (Module.PointOfSale, "pos.write"),
        Permission.PointOfSaleVoid => (Module.PointOfSale, "pos.void"),
        Permission.PointOfSaleShiftOpen => (Module.PointOfSale, "pos.shift.open"),
        Permission.PointOfSaleShiftClose => (Module.PointOfSale, "pos.shift.close"),
        Permission.PointOfSaleShiftReopen => (Module.PointOfSale, "pos.shift.reopen"),
        Permission.PointOfSaleCashMovement => (Module.PointOfSale, "pos.cash.movement"),
        Permission.PointOfSaleSaleDiscount => (Module.PointOfSale, "pos.sale.discount"),
        Permission.PointOfSaleReportRead => (Module.PointOfSale, "pos.report.read")
    };

    // The key of a permission: it is what travels to the front and what the
    // policies of the API are named after.
    public static string Key(this Permission permission) => Describe(permission).Key;

    public static Module ModuleOf(this Permission permission) => Describe(permission).Module;

    // The key of a module: the name the front already uses for it.
    public static string ModuleKey(this Module module) => module switch
    {
        Module.Dashboard => "dashboard",
        Module.Sales => "ventas",
        Module.Purchases => "compras",
        Module.Inventory => "inventario",
        Module.Clients => "clientes",
        Module.Suppliers => "proveedores",
        Module.Accounting => "contabilidad",
        Module.Employees => "rrhh",
        Module.Projects => "proyectos",
        Module.Reports => "reportes",
        Module.Administration => "configuracion",
        Module.ElectronicInvoicing => "facturacion",
        Module.PointOfSale => "pos"
    };

    // Every permission of the catalog. The enum is the catalog, so the list
    // cannot fall behind it.
    public static IReadOnlyList<Permission> All { get; } = Enum.GetValues<Permission>();
}
