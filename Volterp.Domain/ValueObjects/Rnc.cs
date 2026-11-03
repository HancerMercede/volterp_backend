using System.Text.RegularExpressions;
using Volterp.Domain.Enums;
using Volterp.Domain.Exceptions;

namespace Volterp.Domain.ValueObjects;

public readonly partial record struct Rnc
{
    private static readonly int[] CompanyCheckDigitWeights = [7, 9, 8, 6, 5, 4, 3, 2];

    // Cédulas históricas de la JCE que no satisfacen Luhn y siguen siendo documentos válidos.
    // Lista tomada del validador de referencia hminaya/SDQ.
    private static readonly HashSet<string> HistoricalCitizenIds =
    [
        "00000000018", "11111111123", "00100759932", "00105606543", "00114272360",
        "00200123640", "00200409772", "00800106971", "01200004166", "01400074875",
        "01400000282", "03103749672", "03200066940", "03800032522", "03900192284",
        "04900026260", "05900072869", "07700009346", "00114532330", "03121982479",
        "40200700675", "40200639953", "00121581750", "00119161853", "22321581834",
        "00121581800", "09421581768", "22721581818", "90001200901", "00301200901",
        "40200452735", "40200401324", "10621581792"
    ];

    [GeneratedRegex(@"^[0-9\s-]+$")]
    private static partial Regex AllowedCharactersRegex();

    public string Value { get; }
    public RncType Type { get; }

    public Rnc(string value)
    {
        if (!TryParse(value, out var rnc))
            throw new InvalidRncException(value);

        Value = rnc.Value;
        Type = rnc.Type;
    }

    private Rnc(string value, RncType type)
    {
        Value = value;
        Type = type;
    }

    public static bool TryParse(string? value, out Rnc rnc)
    {
        rnc = default;

        if (string.IsNullOrWhiteSpace(value) || !AllowedCharactersRegex().IsMatch(value))
            return false;

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == 9)
        {
            if (!HasValidCompanyCheckDigit(digits))
                return false;

            rnc = new Rnc(digits, RncType.PersonaJuridica);
            return true;
        }

        if (digits.Length == 11)
        {
            if (!HistoricalCitizenIds.Contains(digits) && !HasValidCitizenCheckDigit(digits))
                return false;

            rnc = new Rnc(digits, RncType.PersonaFisica);
            return true;
        }

        return false;
    }

    public override string ToString() => Value;

    private static bool HasValidCompanyCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < CompanyCheckDigitWeights.Length; i++)
            sum += (digits[i] - '0') * CompanyCheckDigitWeights[i];

        var remainder = sum % 11;
        var checkDigit = remainder switch
        {
            0 => 2,
            1 => 1,
            _ => 11 - remainder
        };

        return checkDigit == digits[8] - '0';
    }

    private static bool HasValidCitizenCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var product = (digits[i] - '0') * (i % 2 == 0 ? 1 : 2);
            if (product > 9)
                product -= 9;

            sum += product;
        }

        return (10 - sum % 10) % 10 == digits[10] - '0';
    }
}
