namespace Volterp.Application.Interfaces;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    ICompanyRepository Companies { get; }
    IProductRepository Products { get; }
    ICategoryRepository Categories { get; }
    ISaleRepository Sales { get; }
    ISupplierRepository Suppliers { get; }
    IPurchaseRepository Purchases { get; }
    IEmployeeRepository Employees { get; }
    IAccountingTransactionRepository AccountingTransactions { get; }
    IClientRepository Clients { get; }
    IElectronicInvoiceSequenceRepository ElectronicInvoiceSequences { get; }
    ICompanyCertificateRepository CompanyCertificates { get; }
    IElectronicInvoiceRepository ElectronicInvoices { get; }
    IReceivedElectronicInvoiceRepository ReceivedElectronicInvoices { get; }
    INormalFiscalDocumentRepository NormalFiscalDocuments { get; }
    INormalFiscalSequenceRepository NormalFiscalSequences { get; }
    ICodeGeneratorService Codes { get; }
    IDashboardRepository Dashboard { get; }
    Task<int> CommitAsync(CancellationToken ct = default);

    // The transaction that makes a fiscal number and the document that
    // carries it land together.
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
    void Dispose();
}