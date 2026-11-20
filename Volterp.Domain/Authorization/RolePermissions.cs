using Volterp.Domain.Enums;

namespace Volterp.Domain.Authorization;

// The matrix: which permissions each role grants.
//
// It lives in code and not in the database (S1): git is the audit trail of
// the authorization and the way back. The database only keeps the
// assignment of roles to a user.
//
// The base is the matrix the front has been managing
// (erp-mvp/src/domain/constants/permisos.ts): the same twelve modules, the
// same roles, the same grants. The vocabulary is the one of the plan:
// module × domain action.
public static class RolePermissions
{
    public static IReadOnlyDictionary<UserRole, IReadOnlySet<Permission>> Matrix { get; } = Build();

    // The permissions a role grants. A role that is not in the matrix grants
    // nothing: a user without roles is a user without access.
    public static IReadOnlySet<Permission> For(UserRole role)
        => Matrix.TryGetValue(role, out var permissions) ? permissions : new HashSet<Permission>();

    private static IReadOnlyDictionary<UserRole, IReadOnlySet<Permission>> Build()
    {
        var everything = Enum.GetValues<Permission>().ToHashSet();

        return new Dictionary<UserRole, IReadOnlySet<Permission>>
        {
            [UserRole.SuperAdmin] = everything,
            [UserRole.Admin] = new HashSet<Permission>(everything),

            [UserRole.Ventas] = new HashSet<Permission>
            {
                Permission.DashboardRead,
                Permission.DashboardWrite,
                Permission.SalesRead,
                Permission.SalesWrite,
                Permission.ClientsRead,
                Permission.ClientsWrite,
                Permission.SuppliersRead,
                Permission.ReportsRead,
                Permission.ElectronicInvoicingRead,
                Permission.ElectronicInvoicingWrite,
                Permission.ElectronicInvoicingDelete
            },

            [UserRole.Inventario] = new HashSet<Permission>
            {
                Permission.DashboardRead,
                Permission.DashboardWrite,
                Permission.PurchasesRead,
                Permission.PurchasesWrite,
                Permission.InventoryRead,
                Permission.InventoryWrite,
                Permission.SuppliersRead,
                Permission.SuppliersWrite,
                Permission.ReportsRead
            },

            [UserRole.Contabilidad] = new HashSet<Permission>
            {
                Permission.DashboardRead,
                Permission.DashboardWrite,
                Permission.AccountingRead,
                Permission.AccountingWrite,
                Permission.ReportsRead
            },

            [UserRole.RRHH] = new HashSet<Permission>
            {
                Permission.DashboardRead,
                Permission.DashboardWrite,
                Permission.EmployeesRead,
                Permission.EmployeesWrite,
                Permission.ReportsRead
            }
        };
    }
}
