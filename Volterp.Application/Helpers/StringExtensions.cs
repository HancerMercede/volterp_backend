using System.Text.RegularExpressions;

namespace Volterp.Application.Helpers;

public static partial class StringExtensions
{
    // RFC 5322 simplificado. Cubre ~99% de emails reales.
    // Si alguna vez necesitás el RFC estricto, no es regex lo que querés — es un parser.
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Determines whether the specified string is a syntactically valid email address.
    /// </summary>
    /// <param name="value">The string to validate. May be null or whitespace.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> matches a standard email format
    /// (local part, '@', domain, and a top-level domain with at least two characters);
    /// otherwise, <c>false</c>. Returns <c>false</c> for null, empty, or whitespace strings.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method validates email FORMAT only; it does not verify that the mailbox exists
    /// or that the address is deliverable. Use an external service for that purpose.
    /// </para>
    /// <para>
    /// The pattern is a pragmatic simplification of the address syntax defined in
    /// <see href="https://www.rfc-editor.org/rfc/rfc5322">RFC 5322</see> (Internet Message Format,
    /// Section 3.4.1, <c>addr-spec</c>) and the transmission format from
    /// <see href="https://www.rfc-editor.org/rfc/rfc5321">RFC 5321</see> (Simple Mail Transfer Protocol,
    /// Section 4.1.2). The full RFC grammar supports quoted local parts, comments, IP-literal
    /// domains, and other legacy constructions that are rejected here in favor of practical
    /// coverage of real-world addresses.
    /// </para>
    /// </remarks>
    public static bool IsValidEmail(this string? value)
        => !string.IsNullOrWhiteSpace(value) && EmailRegex().IsMatch(value);
}
