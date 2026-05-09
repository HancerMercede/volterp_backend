namespace Volterp.Api.Helpers;

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
}