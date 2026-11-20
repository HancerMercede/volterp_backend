namespace Volterp.Domain.Authorization;

// The area of work a permission belongs to.
//
// The keys are the ones the front already uses (erp-mvp/src/domain/
// constants/permisos.ts), so the matrix moves to the API without renaming
// anything the front already knows.
public enum Module
{
    Dashboard,
    Sales,
    Purchases,
    Inventory,
    Clients,
    Suppliers,
    Accounting,
    Employees,
    Projects,
    Reports,
    Administration,
    ElectronicInvoicing,
    PointOfSale
}
