using Volterp.Application.Interfaces;
using Volterp.Application.Services;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;

namespace Volterp.Infrastructure.UnitOfWork;

public class UnitOfWork(VolterpDbContext context) : IUnitOfWork
{
    private readonly Lazy<IUserRepository> _users = new(() => new UserRepository(context));
    private readonly Lazy<ICompanyRepository> _companies = new(() => new CompanyRepository(context));
    private readonly Lazy<IProductRepository> _products = new(() => new ProductRepository(context));
    private readonly Lazy<ICategoryRepository> _categories = new(() => new CategoryRepository(context));
    private readonly Lazy<ISaleRepository> _sales = new(() => new SaleRepository(context));
    private readonly Lazy<ISupplierRepository> _suppliers = new(() => new SupplierRepository(context));
    private readonly Lazy<IPurchaseRepository> _purchases = new(() => new PurchaseRepository(context));
    private readonly Lazy<IEmployeeRepository> _employees = new(() => new EmployeeRepository(context));
    private readonly Lazy<IAccountingTransactionRepository> _accountingTransactions = new(() => new AccountingTransactionRepository(context));
    private readonly Lazy<IClientRepository> _clients = new(() => new ClientRepository(context));
    private readonly Lazy<IDashboardRepository> _dashboard = new(() => new DashboardRepository(context));
    private readonly Lazy<IElectronicInvoiceSequenceRepository> _electronicInvoiceSequences = new(() => new ElectronicInvoiceSequenceRepository(context));
    private readonly Lazy<ICompanyCertificateRepository> _companyCertificates = new(() => new CompanyCertificateRepository(context));
    private readonly Lazy<ICompanyIssuerRepository> _companyIssuers = new(() => new CompanyIssuerRepository(context));
    private readonly Lazy<IElectronicInvoiceRepository> _electronicInvoices = new(() => new ElectronicInvoiceRepository(context));
    private readonly Lazy<IReceivedElectronicInvoiceRepository> _receivedElectronicInvoices = new(() => new ReceivedElectronicInvoiceRepository(context));
    private readonly Lazy<INormalFiscalDocumentRepository> _normalFiscalDocuments = new(() => new NormalFiscalDocumentRepository(context));
    private readonly Lazy<INormalFiscalSequenceRepository> _normalFiscalSequences = new(() => new NormalFiscalSequenceRepository(context));
    private readonly Lazy<ICodeGeneratorService> _codes = new(() => new CodeGeneratorService(new CodeSequenceRepository(context)));
    private readonly Lazy<IApiCredentialRepository> _apiCredentials = new(() => new ApiCredentialRepository(context));

    public IUserRepository Users => _users.Value;
    public ICompanyRepository Companies => _companies.Value;
    public IProductRepository Products => _products.Value;
    public ICategoryRepository Categories => _categories.Value;
    public ISaleRepository Sales => _sales.Value;
    public ISupplierRepository Suppliers => _suppliers.Value;
    public IPurchaseRepository Purchases => _purchases.Value;
    public IEmployeeRepository Employees => _employees.Value;
    public IAccountingTransactionRepository AccountingTransactions => _accountingTransactions.Value;
    public IClientRepository Clients => _clients.Value;
    public IDashboardRepository Dashboard => _dashboard.Value;
    public IElectronicInvoiceSequenceRepository ElectronicInvoiceSequences => _electronicInvoiceSequences.Value;
    public ICompanyCertificateRepository CompanyCertificates => _companyCertificates.Value;
    public ICompanyIssuerRepository CompanyIssuers => _companyIssuers.Value;
    public IElectronicInvoiceRepository ElectronicInvoices => _electronicInvoices.Value;
    public IReceivedElectronicInvoiceRepository ReceivedElectronicInvoices => _receivedElectronicInvoices.Value;
    public INormalFiscalDocumentRepository NormalFiscalDocuments => _normalFiscalDocuments.Value;
    public INormalFiscalSequenceRepository NormalFiscalSequences => _normalFiscalSequences.Value;
    public ICodeGeneratorService Codes => _codes.Value;
    public IApiCredentialRepository ApiCredentials => _apiCredentials.Value;

    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        var affected = await context.SaveChangesAsync(ct);

        // A flow that allocated a business code opened a transaction before the
        // insert; committing it here makes the code and its row land together.
        var transaction = context.Database.CurrentTransaction;
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
            await transaction.DisposeAsync();
        }

        return affected;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        // The fiscal number and the document that carries it have to land
        // together, so the transaction is opened before the number is taken.
        if (context.Database.CurrentTransaction is null)
            await context.Database.BeginTransactionAsync(ct);
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        var transaction = context.Database.CurrentTransaction;

        if (transaction is null)
            return;

        await transaction.RollbackAsync(ct);
        await transaction.DisposeAsync();
    }

    public void Dispose() => context.Dispose();
}