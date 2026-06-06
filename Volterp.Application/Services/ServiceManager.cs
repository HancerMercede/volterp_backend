using Volterp.Application.Interfaces;

namespace Volterp.Application.Services;

public class ServiceManager(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IServiceManager
{
    private IProductService? _products;
    public IProductService Products => _products ??= new ProductService(unitOfWork);

    private ICategoryService? _categories;
    public ICategoryService Categories => _categories ??= new CategoryService(unitOfWork);

    private IUserService? _users;
    public IUserService Users => _users ??= new UserService(unitOfWork, passwordHasher);
    
    private ICompanyService? _companies;
    public ICompanyService Companies => _companies  ??= new CompanyService(unitOfWork);

    private ISaleService? _sales;
    public ISaleService Sales => _sales ??= new SaleService(unitOfWork);

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
}