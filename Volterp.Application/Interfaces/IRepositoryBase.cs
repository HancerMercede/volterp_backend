using System.Linq.Expressions;
using Volterp.Application.Helpers;

namespace Volterp.Application.Interfaces;

public interface IRepositoryBase<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PagedResult<T>> GetAllAsync(Expression<Func<T, bool>> predicate, int pageNumber, int PageSize, CancellationToken ct = default);
    Task<T?> GetByCondictionsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}