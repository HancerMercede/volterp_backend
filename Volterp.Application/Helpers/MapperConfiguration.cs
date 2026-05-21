namespace Volterp.Application.Helpers;

/// <summary>
/// Lightweight mapping helpers. Zero reflection, zero overhead.
/// </summary>
public static class MapperConfiguration
{
    /// <summary>
    /// Maps a single object to a new type via a selector.
    /// </summary>
    /// <example>
    /// var dto = product.Map(p => new ProductDto(p.Id, p.Name, p.Price));
    /// </example>
    public static TDestination Map<TSource, TDestination>(
        this TSource source,
        Func<TSource, TDestination> selector) => selector(source);

    /// <summary>
    /// Maps a collection to a new type. Prefer .Select() for simple projections.
    /// Use this when the selector has conditionals, lookups, or non-trivial logic.
    /// </summary>
    /// <example>
    /// var dtos = products.Map(p => new ProductDto(p.Id, p.Name, p.IsActive ? "Active" : "Inactive"));
    /// </example>
    public static List<TDestination> Map<TSource, TDestination>(
        this IEnumerable<TSource> source,
        Func<TSource, TDestination> selector) => source.Select(selector).ToList();

    /// <summary>
    /// Maps a PagedResult to a new type, preserving pagination metadata.
    /// </summary>
    /// <example>
    /// var dtos = pagedCategories.Map(c => new CategoryDto(c.Id, c.Name));
    /// </example>
    public static PagedResult<TDestination> Map<TSource, TDestination>(
        this PagedResult<TSource> source,
        Func<TSource, TDestination> selector)
        => new()
        {
            Items = source.Items.Select(selector).ToList(),
            RowCount = source.RowCount,
            PageCount = source.PageCount,
            PageNumber = source.PageNumber,
            PageSize = source.PageSize
        };
    /// <summary>
    /// Mutates an object in place and returns the same instance.
    /// Use for updates — Apply() modifies, Map() projects.
    /// </summary>
    /// <example>
    /// await repo.UpdateAsync(product.Apply(p => {
    ///     p.Name      = request.Name;
    ///     p.UpdatedAt = DateTime.UtcNow;
    /// }), ct);
    /// </example>
    public static TSource Apply<TSource>(this TSource source, Action<TSource> mutator)
    {
        mutator(source);
        return source;
    }
    /// <summary>
    /// Transforms an object via a function and returns the result.
    /// Use when the transformation may produce a new instance or apply complex logic.
    /// </summary>
    /// <example>
    /// var transformed = product.Apply(p => new Product(p.Id, request.Name, p.Price));
    /// </example>
    public static TSource Apply<TSource>(this TSource source, Func<TSource, TSource> transform)
        => transform(source);
    
    
}