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
}