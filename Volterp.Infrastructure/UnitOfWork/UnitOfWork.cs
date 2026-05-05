using Volterp.Application.Interfaces;
using Volterp.Infrastructure.Data;
using Volterp.Infrastructure.Repositories;

namespace Volterp.Infrastructure.UnitOfWork;

public class UnitOfWork(VolterpDbContext context) : IUnitOfWork
{
    private readonly Lazy<IUserRepository> _users = new(() => new UserRepository(context));
    private readonly Lazy<ICompanyRepository> _companies = new(() => new CompanyRepository(context));
    private readonly Lazy<IProductRepository> _products = new(() => new ProductRepository(context));
    private readonly Lazy<ICategoryRepository> _categories = new(() => new CategoryRepository(context));

    public IUserRepository Users => _users.Value;
    public ICompanyRepository Companies => _companies.Value;
    public IProductRepository Products => _products.Value;
    public ICategoryRepository Categories => _categories.Value;

    public async Task<int> CommitAsync(CancellationToken ct = default) => await context.SaveChangesAsync(ct);

    public void Dispose() => context.Dispose();
}