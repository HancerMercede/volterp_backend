namespace Volterp.Domain.Authorization;

// The closed catalog of permissions: module × domain action.
//
// Closed means closed: a permission that is not in this enum cannot be
// granted, so no typo can open a door. The action is a domain action and
// not a generic CRUD on purpose: the POS has to tell "anular a venta" from
// "cerrar un turno" to know who did what.
//
// The base vocabulary (read, write, delete per module) is the one the front
// has been managing; the actions of domain (contabilidad.post, the set of
// the POS) are the ones the plan of the feature adds.
public enum Permission
{
    DashboardRead,
    DashboardWrite,
    DashboardDelete,

    SalesRead,
    SalesWrite,
    SalesDelete,

    PurchasesRead,
    PurchasesWrite,
    PurchasesDelete,

    InventoryRead,
    InventoryWrite,
    InventoryDelete,

    ClientsRead,
    ClientsWrite,
    ClientsDelete,

    SuppliersRead,
    SuppliersWrite,
    SuppliersDelete,

    AccountingRead,
    AccountingWrite,
    AccountingDelete,

    // Posting an entry is not the same as editing the accounting: it moves
    // the books, and the audit asks who did it.
    AccountingPost,

    EmployeesRead,
    EmployeesWrite,
    EmployeesDelete,

    ProjectsRead,
    ProjectsWrite,
    ProjectsDelete,

    ReportsRead,
    ReportsWrite,
    ReportsDelete,

    AdministrationRead,
    AdministrationWrite,
    AdministrationDelete,

    ElectronicInvoicingRead,
    ElectronicInvoicingWrite,
    ElectronicInvoicingDelete,

    PointOfSaleRead,
    PointOfSaleWrite,
    PointOfSaleVoid,
    PointOfSaleShiftOpen,
    PointOfSaleShiftClose,
    PointOfSaleShiftReopen,
    PointOfSaleCashMovement,
    PointOfSaleSaleDiscount,
    PointOfSaleReportRead
}
