using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Volterp.Application.Helpers;
using Volterp.Application.Interfaces;
using Volterp.Infrastructure.Data;

namespace Volterp.Infrastructure.Repositories;

public abstract class RepositoryBase<T>(VolterpDbContext context) : IRepositoryBase<T>
    where T : class
{
    protected DbSet<T> Set() => context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await Set().FindAsync([id], ct);

    public virtual async Task<PagedResult<T>> GetAllAsync(Expression<Func<T, bool>> predicate,int pageNumber, int pageSize,
        CancellationToken ct = default)
        => await Set().Where(predicate)
            .AsNoTracking()
            .ToPagedResultAsync(pageNumber, pageSize, ct);

    public async Task<T?> GetByCondictionsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => 
        await Set().Where(predicate).FirstOrDefaultAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)=>
        await Set().AddAsync(entity, ct);

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)=>
    await Task.FromResult(Set().Update(entity));

    public virtual async Task DeleteAsync(T entity, CancellationToken ct = default)=>
        await Task.FromResult(Set().Remove(entity));
    
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await Set().AnyAsync(predicate, ct);
}