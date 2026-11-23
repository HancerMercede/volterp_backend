namespace Volterp.Application.Interfaces;

public interface IServiceManager
{
    IProductService Products { get; }
    ICategoryService Categories { get; }
    IUserService Users { get; }
    ICompanyService Companies { get; }
    ISaleService Sales { get; }
    ISupplierService Suppliers { get; }
    IPurchaseService Purchases { get; }
    IEmployeeService Employees { get; }
    IAccountingTransactionService AccountingTransactions { get; }
    IClientService Clients { get; }
    ICompanyCertificateService CompanyCertificates { get; }
    ICompanyIssuerService CompanyIssuers { get; }
    IElectronicInvoiceSequenceService ElectronicInvoiceSequences { get; }
    IElectronicInvoiceService ElectronicInvoices { get; }
    IElectronicInvoicePrintService ElectronicInvoicePrints { get; }
    IElectronicInvoiceReceptionService ElectronicInvoiceReceptions { get; }
    INormalFiscalSequenceService NormalFiscalSequences { get; }
    ICompanyEmissionModeService CompanyEmissionModes { get; }
    INormalFiscalDocumentService NormalFiscalDocuments { get; }
    IDashboardService Dashboard { get; }
    IApiCredentialService ApiCredentials { get; }
}