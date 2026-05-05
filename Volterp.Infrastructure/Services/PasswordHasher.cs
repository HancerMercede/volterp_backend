using System.Security.Cryptography;
using System.Text;
using Volterp.Application.Interfaces;

namespace Volterp.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const string BcryptPrefix = "$2";

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 12);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;

        if (hash.StartsWith(BcryptPrefix))
            return BCrypt.Net.BCrypt.Verify(password, hash);

        return LegacyVerify(password, hash);
    }

    private static bool LegacyVerify(string password, string hash)
    {
        if (password == hash) return true;

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        var computedHash = Convert.ToBase64String(bytes);
        return computedHash == hash;
    }
}