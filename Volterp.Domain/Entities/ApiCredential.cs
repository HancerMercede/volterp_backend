using System.Security.Cryptography;
using System.Text;
using Volterp.Domain.Abstractions;
using Volterp.Domain.Enums;

namespace Volterp.Domain.Entities;

// The credential of a client that rents the API. It has two halves: a
// public selector that says which credential is being presented and a secret
// that only the client knows. The secret is never stored: what is stored is
// its hash, and the secret travels only in the answer of the creation.
public class ApiCredential : AuditEntity, ITenantScoped
{
    public const int KeyIdLength = 12;
    public const int SecretLengthInBytes = 32;

    // The selector is read out loud and typed by hand: the letters that can
    // be confused with each other are not in it.
    private const string KeyIdAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string SecretHash { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;

    public bool IsExpired => ExpiresAt is not null && ExpiresAt.Value <= DateTime.UtcNow;

    // The revocation does not delete the row: what was issued matters.
    public void Revoke() => Status = EntityStatus.Inactive;

    public static ApiCredentialIssued Create(Guid companyId, string name, DateTime? expiresAt)
    {
        var secret = NewSecret();

        var credential = new ApiCredential
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            Name = name,
            KeyId = NewKeyId(),
            SecretHash = HashSecret(secret),
            ExpiresAt = expiresAt
        };

        return new ApiCredentialIssued(credential, secret);
    }

    public static string HashSecret(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    private static string NewKeyId()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyIdLength);
        var chars = new char[KeyIdLength];

        // The alphabet has 32 symbols and the byte has 256 values: the
        // remainder does not favor any symbol.
        for (var i = 0; i < KeyIdLength; i++)
            chars[i] = KeyIdAlphabet[bytes[i] % KeyIdAlphabet.Length];

        return new string(chars);
    }

    private static string NewSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretLengthInBytes);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

// The credential and the secret that is shown once. The secret does not go
// back into the entity: it is not stored anywhere.
public sealed record ApiCredentialIssued(ApiCredential Credential, string Secret);
