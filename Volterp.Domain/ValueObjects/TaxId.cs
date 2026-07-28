using System.Text.RegularExpressions;

namespace Volterp.Domain.ValueObjects;

/// <summary>
/// Tax Identifier value object — validates 8-15 alphanumeric characters, normalized to uppercase.
/// </summary>
public readonly partial record struct TaxId
{
    [GeneratedRegex("^[A-Z0-9]+$")]
    private static partial Regex AlphanumericRegex();

    public string Value { get; }

    public TaxId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Tax ID cannot be null, empty, or whitespace.", nameof(value));

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length < 8 || normalized.Length > 15)
            throw new ArgumentException(
                $"Tax ID must be between 8 and 15 characters. Got {normalized.Length}.",
                nameof(value));

        if (!AlphanumericRegex().IsMatch(normalized))
            throw new ArgumentException(
                $"'{value}' is not a valid Tax ID. Only alphanumeric characters are allowed.",
                nameof(value));

        Value = normalized;
    }

    public override string ToString() => Value;
}
