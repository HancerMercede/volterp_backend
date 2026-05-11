using Microsoft.EntityFrameworkCore;
namespace Volterp.Application.Helpers;

public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> source, int pageNumber,
        int pageSize, CancellationToken token) where T : class
    {
        var rowCount = await source.CountAsync();
        var pageCount  = (int)Math.Ceiling(rowCount / (double)pageSize);
        var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<T>
        {
            RowCount = rowCount,
            PageNumber = pageNumber,
            PageCount = pageCount,
            PageSize = pageSize,
            Items = items
        };
    }
}