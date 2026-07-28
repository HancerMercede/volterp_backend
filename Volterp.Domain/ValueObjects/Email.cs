using System.Text.RegularExpressions;

namespace Volterp.Domain.ValueObjects;

public readonly partial record struct Email
{
    // Same pattern as Volterp.Application.Helpers.StringExtensions.IsValidEmail.
    // Pattern is duplicated intentionally — Domain cannot reference Application,
    // and both layers need email validation.
    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailRegex();

    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be null, empty, or whitespace.", nameof(value));

        value = value.Trim();

        if (!EmailRegex().IsMatch(value))
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));

        Value = value;
    }

    public override string ToString() => Value;
}
