namespace Volterp.Application.Interfaces;

public interface IUnitOfWork
{
    IUserRepository Users { get; }
    ICompanyRepository Companies { get; }
    IProductRepository Products { get; }
    ICategoryRepository Categories { get; }
    ISaleRepository Sales { get; }
    ISupplierRepository Suppliers { get; }
    Task<int> CommitAsync(CancellationToken ct = default);
    void Dispose();
}