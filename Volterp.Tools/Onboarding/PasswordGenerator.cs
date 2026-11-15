using System.Security.Cryptography;

namespace Volterp.Tools.Onboarding;

// The password of the first administrator of a tenant is generated, never
// typed by the operator: a password that someone else knows is a password that
// somebody else can use.
public sealed class PasswordGenerator : IPasswordGenerator
{
    // The alphabet leaves out the characters that are easy to confuse when the
    // password is read from a screen (0 and O, 1 and l) and the ones that break
    // a shell or an attribute.
    private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string Symbols = "!@#%^*_-+=";
    private const int Length = 24;

    public string Generate()
    {
        var alphabet = Lowercase + Uppercase + Digits + Symbols;

        var characters = new List<char>(Length)
        {
            Lowercase[RandomNumberGenerator.GetInt32(Lowercase.Length)],
            Uppercase[RandomNumberGenerator.GetInt32(Uppercase.Length)],
            Digits[RandomNumberGenerator.GetInt32(Digits.Length)],
            Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)]
        };

        while (characters.Count < Length)
            characters.Add(alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]);

        for (var i = characters.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[j]) = (characters[j], characters[i]);
        }

        return new string(characters.ToArray());
    }
}
