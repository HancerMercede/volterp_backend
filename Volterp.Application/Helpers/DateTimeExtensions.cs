namespace Volterp.Application.Helpers;

/// <summary>
/// Helpers for normalizing <see cref="DateTime"/> values that originate from external sources
/// (DTOs, JSON deserialization, etc.) before they reach the domain layer.
/// </summary>
/// <remarks>
/// <para>
/// Npgsql 8.x is strict: PostgreSQL <c>timestamp with time zone</c> columns only accept
/// <see cref="DateTime"/> values with <see cref="DateTimeKind.Utc"/>. Values deserialized
/// from JSON typically arrive with <see cref="DateTimeKind.Unspecified"/>, causing
/// <see cref="System.ArgumentException"/> at save time.
/// </para>
/// <para>
/// These extensions enforce the UTC invariant at the application boundary so the domain
/// layer can assume any <see cref="DateTime"/> it receives is already UTC-kind.
/// </para>
/// </remarks>
public static class DateTimeExtensions
{
    /// <summary>
    /// Returns the <see cref="DateTime"/> re-tagged as <see cref="DateTimeKind.Utc"/>
    /// without altering its tick value.
    /// </summary>
    /// <param name="value">The input value. Its tick value is preserved.</param>
    /// <returns>
    /// A new <see cref="DateTime"/> with the same ticks as <paramref name="value"/>
    /// and <see cref="DateTimeKind.Utc"/>.
    /// </returns>
    public static DateTime AsUtc(this DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>
    /// Returns the nullable <see cref="DateTime"/> re-tagged as <see cref="DateTimeKind.Utc"/>
    /// when it has a value; otherwise <c>null</c>.
    /// </summary>
    /// <param name="value">The input value. Its tick value is preserved when present.</param>
    /// <returns>
    /// A new nullable <see cref="DateTime"/> with the same ticks as <paramref name="value"/>
    /// and <see cref="DateTimeKind.Utc"/>, or <c>null</c> if the input had no value.
    /// </returns>
    public static DateTime? AsUtc(this DateTime? value)
        => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}