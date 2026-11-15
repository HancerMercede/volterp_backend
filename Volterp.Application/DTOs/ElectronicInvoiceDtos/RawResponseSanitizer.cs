using System.Text.RegularExpressions;

namespace Volterp.Application.DTOs.ElectronicInvoiceDtos;

// The body of the DGII is raw text that comes from outside: it can carry a
// credential (the token of the DGII travels in the conversation of the
// token) and it can carry markup (the HTML of a gateway). What leaves the
// API never carries a credential.
internal static class RawResponseSanitizer
{
    // The value of an authorization header that a gateway echoed.
    private static readonly Regex BearerValue = new(
        """(?<=bearer\s)[^\s"'<>]+""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // A field whose name says that it carries a credential, and the value
    // that follows it.
    private static readonly Regex CredentialField = new(
        """(?<prefix>["']?(?:token|access[_-]?token|refresh[_-]?token|authorization|password|secret|api[_-]?key)["']?\s*[:=]\s*["']?)[^"'\s,;&}\]]+""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string? Redact(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return body;

        // The bearer goes first: the value of an authorization header is a
        // credential even when the field that carries it is named.
        return CredentialField.Replace(BearerValue.Replace(body, "[redacted]"), "${prefix}[redacted]");
    }
}
