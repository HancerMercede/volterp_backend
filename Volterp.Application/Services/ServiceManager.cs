using Volterp.Application.Interfaces;

namespace Volterp.Application.Services;

public class ServiceManager(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher, ICurrentUserService currentUserService, ICredentialProtector credentialProtector, IElectronicInvoiceService electronicInvoices) : IServiceManager
{
    private IProductService? _products;
    public IProductService Products => _products ??= new ProductService(unitOfWork);

    private ICategoryService? _categories;
    public ICategoryService Categories => _categories ??= new CategoryService(unitOfWork);

    private IUserService? _users;
    public IUserService Users => _users ??= new UserService(unitOfWork, passwordHasher, currentUserService);
    
    private ICompanyService? _companies;
    public ICompanyService Companies => _companies  ??= new CompanyService(unitOfWork);

    private ISaleService? _sales;
    public ISaleService Sales => _sales ??= new SaleService(unitOfWork, electronicInvoices, NormalFiscalDocuments);

    private ISupplierService? _suppliers;
    public ISupplierService Suppliers => _suppliers ??= new SupplierService(unitOfWork);

    private IPurchaseService? _purchases;
    public IPurchaseService Purchases => _purchases ??= new PurchaseService(unitOfWork);

    private IEmployeeService? _employees;
    public IEmployeeService Employees => _employees ??= new EmployeeService(unitOfWork);

    private IAccountingTransactionService? _accountingTransactions;
    public IAccountingTransactionService AccountingTransactions => _accountingTransactions ??= new AccountingTransactionService(unitOfWork);

    private IClientService? _clients;
    public IClientService Clients => _clients ??= new ClientService(unitOfWork);

    private ICompanyCertificateService? _companyCertificates;
    public ICompanyCertificateService CompanyCertificates => _companyCertificates ??= new CompanyCertificateService(unitOfWork, credentialProtector);
    
    private ICompanyIssuerService? _companyIssuers;
    public ICompanyIssuerService CompanyIssuers => _companyIssuers ??= new CompanyIssuerService(unitOfWork);

    private IElectronicInvoiceSequenceService? _electronicInvoiceSequences;
    public IElectronicInvoiceSequenceService ElectronicInvoiceSequences => _electronicInvoiceSequences ??= new ElectronicInvoiceSequenceService(unitOfWork);

    public IElectronicInvoiceService ElectronicInvoices => electronicInvoices;

    private IDashboardService? _dashboard;
    public IDashboardService Dashboard => _dashboard ??= new DashboardService(unitOfWork);

    private INormalFiscalDocumentService? _normalFiscalDocuments;
    public INormalFiscalDocumentService NormalFiscalDocuments => _normalFiscalDocuments ??= new NormalFiscalDocumentService(unitOfWork);

    private IElectronicInvoicePrintService? _electronicInvoicePrints;
    public IElectronicInvoicePrintService ElectronicInvoicePrints => _electronicInvoicePrints ??= new ElectronicInvoicePrintService(unitOfWork);

    private IElectronicInvoiceReceptionService? _electronicInvoiceReceptions;
    public IElectronicInvoiceReceptionService ElectronicInvoiceReceptions => _electronicInvoiceReceptions ??= new ElectronicInvoiceReceptionService(unitOfWork);

    private INormalFiscalSequenceService? _normalFiscalSequences;
    public INormalFiscalSequenceService NormalFiscalSequences => _normalFiscalSequences ??= new NormalFiscalSequenceService(unitOfWork);

    private ICompanyEmissionModeService? _companyEmissionModes;
    public ICompanyEmissionModeService CompanyEmissionModes => _companyEmissionModes ??= new CompanyEmissionModeService(unitOfWork);
}