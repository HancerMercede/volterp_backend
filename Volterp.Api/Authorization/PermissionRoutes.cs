using Volterp.Domain.Authorization;

namespace Volterp.Api.Authorization;

// What every route of the API needs.
//
// The permission of a route is not written in the route: it is here, in one
// place, and the matrix (the roles and what they grant) is what decides who
// gets in. A route that this map does not cover is closed.
//
// The key is the controller and the method of the route. The routes that need
// something else carry their own entry, by the name of their action.
public static class PermissionRoutes
{
    private static readonly IReadOnlyDictionary<string, Permission> ByControllerAndMethod =
        new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase)
        {
            // El panel: lo que todos los roles de trabajo pueden leer.
            ["Dashboard:GET"] = Permission.DashboardRead,

            // Ventas.
            ["Sales:GET"] = Permission.SalesRead,
            ["Sales:POST"] = Permission.SalesWrite,
            ["Sales:PUT"] = Permission.SalesWrite,
            ["Sales:DELETE"] = Permission.SalesDelete,

            // Compras.
            ["Purchases:GET"] = Permission.PurchasesRead,
            ["Purchases:POST"] = Permission.PurchasesWrite,
            ["Purchases:PUT"] = Permission.PurchasesWrite,
            ["Purchases:DELETE"] = Permission.PurchasesDelete,

            // Inventario.
            ["Products:GET"] = Permission.InventoryRead,
            ["Products:POST"] = Permission.InventoryWrite,
            ["Products:PUT"] = Permission.InventoryWrite,
            ["Products:DELETE"] = Permission.InventoryDelete,
            ["Categories:GET"] = Permission.InventoryRead,
            ["Categories:POST"] = Permission.InventoryWrite,
            ["Categories:PUT"] = Permission.InventoryWrite,
            ["Categories:DELETE"] = Permission.InventoryDelete,

            // Clientes y proveedores.
            ["Clients:GET"] = Permission.ClientsRead,
            ["Clients:POST"] = Permission.ClientsWrite,
            ["Clients:PUT"] = Permission.ClientsWrite,
            ["Clients:DELETE"] = Permission.ClientsDelete,
            ["Suppliers:GET"] = Permission.SuppliersRead,
            ["Suppliers:POST"] = Permission.SuppliersWrite,
            ["Suppliers:PUT"] = Permission.SuppliersWrite,
            ["Suppliers:DELETE"] = Permission.SuppliersDelete,

            // Contabilidad. Asentar un movimiento es una accion propia.
            ["AccountingTransactions:GET"] = Permission.AccountingRead,
            ["AccountingTransactions:POST"] = Permission.AccountingPost,
            ["AccountingTransactions:PUT"] = Permission.AccountingWrite,
            ["AccountingTransactions:DELETE"] = Permission.AccountingDelete,

            // Recursos humanos.
            ["Employees:GET"] = Permission.EmployeesRead,
            ["Employees:POST"] = Permission.EmployeesWrite,
            ["Employees:PUT"] = Permission.EmployeesWrite,
            ["Employees:DELETE"] = Permission.EmployeesDelete,

            // Configuracion: la empresa, los usuarios y las credenciales.
            ["Companies:GET"] = Permission.AdministrationRead,
            ["Companies:POST"] = Permission.AdministrationWrite,
            ["Companies:PUT"] = Permission.AdministrationWrite,
            ["Companies:DELETE"] = Permission.AdministrationDelete,
            ["Users:GET"] = Permission.AdministrationRead,
            ["Users:POST"] = Permission.AdministrationWrite,
            ["Users:PUT"] = Permission.AdministrationWrite,
            ["Users:DELETE"] = Permission.AdministrationDelete,
            ["ApiCredentials:GET"] = Permission.AdministrationRead,
            ["ApiCredentials:POST"] = Permission.AdministrationWrite,
            ["ApiCredentials:DELETE"] = Permission.AdministrationDelete,

            // Facturacion electronica: emisores, rangos, certificados, el modo
            // de emision y los comprobantes.
            ["CompanyIssuers:GET"] = Permission.ElectronicInvoicingRead,
            ["CompanyIssuers:POST"] = Permission.ElectronicInvoicingWrite,
            ["CompanyIssuers:PUT"] = Permission.ElectronicInvoicingWrite,
            ["CompanyCertificates:GET"] = Permission.ElectronicInvoicingRead,
            ["CompanyCertificates:POST"] = Permission.ElectronicInvoicingWrite,
            ["ElectronicInvoiceSequences:GET"] = Permission.ElectronicInvoicingRead,
            ["ElectronicInvoiceSequences:POST"] = Permission.ElectronicInvoicingWrite,
            ["ElectronicInvoiceSequences:PUT"] = Permission.ElectronicInvoicingWrite,
            ["NormalFiscalSequences:GET"] = Permission.ElectronicInvoicingRead,
            ["NormalFiscalSequences:POST"] = Permission.ElectronicInvoicingWrite,
            ["NormalFiscalSequences:PUT"] = Permission.ElectronicInvoicingWrite,
            ["ElectronicInvoices:GET"] = Permission.ElectronicInvoicingRead,
            ["ElectronicInvoices:POST"] = Permission.ElectronicInvoicingWrite,
            ["ElectronicInvoicePrint:GET"] = Permission.ElectronicInvoicingRead,
            ["ElectronicInvoiceReception:POST"] = Permission.ElectronicInvoicingWrite,
            ["CompanyEmissionMode:GET"] = Permission.ElectronicInvoicingRead,
            ["CompanyEmissionMode:PUT"] = Permission.ElectronicInvoicingWrite,

            // El registro de un usuario (la API alquilada lo usa).
            ["Auth:POST"] = Permission.AdministrationWrite,

            // La matriz de los roles (la lista de permisos de cada rol).
            ["Permissions:GET"] = Permission.AdministrationRead
        };

    // The routes that only need a caller with a token: the route that answers
    // what the caller may do cannot ask for a permission to be asked.
    private static readonly IReadOnlySet<string> OnlyAuthenticated =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Permissions.GetMe"
        };

    // The permission of a route that needs something else, by the name of its
    // action.
    private static readonly IReadOnlyDictionary<string, Permission> ByControllerAndAction =
        new Dictionary<string, Permission>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the route only needs a caller with a token.
    /// </summary>
    public static bool NeedsOnlyAToken(Type controllerType, string? actionName = null)
    {
        if (actionName is null)
            return false;

        return OnlyAuthenticated.Contains($"{NameOf(controllerType)}.{actionName}");
    }

    /// <summary>
    /// The permission a route needs, or null when the map does not cover it.
    /// </summary>
    public static Permission? For(Type controllerType, string httpMethod, string? actionName = null)
    {
        var controller = NameOf(controllerType);

        if (actionName is not null
            && ByControllerAndAction.TryGetValue($"{controller}.{actionName}", out var byAction))
            return byAction;

        return ByControllerAndMethod.TryGetValue($"{controller}:{httpMethod.ToUpperInvariant()}", out var byMethod)
            ? byMethod
            : null;
    }

    private static string NameOf(Type controllerType)
        => controllerType.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controllerType.Name[..^"Controller".Length]
            : controllerType.Name;

    /// <summary>
    /// Every entry of the map, as "Controller:VERB" or "Controller.Action".
    /// </summary>
    public static IReadOnlyCollection<string> Entries { get; } =
        ByControllerAndMethod.Keys.Concat(ByControllerAndAction.Keys).ToArray();
}