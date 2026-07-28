using System.Text.RegularExpressions;
using Volterp.Domain.Exceptions;

namespace Volterp.Domain.ValueObjects;

/// <summary>
/// Tax Identifier value object — validates 8-15 alphanumeric characters.
/// No normalization applied; the value is stored as provided.
/// </summary>
public readonly partial record struct TaxId
{
    [GeneratedRegex("^[A-Za-z0-9]+$")]
    private static partial Regex AlphanumericRegex();

    public string Value { get; }

    public TaxId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidTaxIdException(value);

        if (value.Length < 8 || value.Length > 15)
            throw new InvalidTaxIdException(value);

        if (!AlphanumericRegex().IsMatch(value))
            throw new InvalidTaxIdException(value);

        Value = value;
    }

    public override string ToString() => Value;
}