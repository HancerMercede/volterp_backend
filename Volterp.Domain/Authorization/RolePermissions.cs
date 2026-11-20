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

    // The permissions of the roles a user holds: the union of them. The
    // effective permissions of a user are never less than the ones of any of
    // the roles it holds.
    public static IReadOnlySet<Permission> For(IEnumerable<UserRole> roles)
    {
        var permissions = new HashSet<Permission>();

        foreach (var role in roles)
            permissions.UnionWith(For(role));

        return permissions;
    }

    private static IReadOnlyDictionary<UserRole, IReadOnlySet<Permission>> Build()
    {
        var everything = Enum.GetValues<Permission>().ToHashSet();

        return new Dictionary<UserRole, IReadOnlySet<Permission>>
        {
            [UserRole.SuperAdmin] = everything,
            [UserRole.Admin] = new HashSet<Permission>(everything),

            // The sales role reads the clients and the fiscal module, but
            // it does not write them: the API has always reserved the writing
            // of the clients and of the fiscal configuration (issuers, ranges,
            // certificates, emission mode, credentials) to the admins. The
            // matrix says what the API does, not what the front used to offer.
            [UserRole.Ventas] = new HashSet<Permission>
            {
                Permission.DashboardRead,
                Permission.DashboardWrite,
                Permission.SalesRead,
                Permission.SalesWrite,
                Permission.ClientsRead,
                Permission.SuppliersRead,
                Permission.ReportsRead,
                Permission.ElectronicInvoicingRead
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
