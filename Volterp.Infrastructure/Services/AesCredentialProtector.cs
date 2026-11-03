using System.Security.Cryptography;
using System.Text;
using Volterp.Application.Interfaces;

namespace Volterp.Infrastructure.Services;

public class AesCredentialProtector : ICredentialProtector
{
    private const string Prefix = "v1:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _masterKey;

    public AesCredentialProtector(string masterKey)
    {
        if (string.IsNullOrWhiteSpace(masterKey))
            throw new InvalidOperationException("The encryption master key is not configured.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(masterKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("The encryption master key must be a base64-encoded 256-bit key.");
        }

        if (key.Length != KeySize)
            throw new InvalidOperationException("The encryption master key must decode to exactly 32 bytes.");

        _masterKey = key;
    }

    public string Protect(Guid companyId, string plaintext)
    {
        var key = DeriveKey(companyId);

        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            var payload = new byte[NonceSize + TagSize + cipherBytes.Length];
            nonce.CopyTo(payload, 0);
            tag.CopyTo(payload, NonceSize);
            cipherBytes.CopyTo(payload, NonceSize + TagSize);

            return Prefix + Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public string Unprotect(Guid companyId, string protectedValue)
    {
        var key = DeriveKey(companyId);

        try
        {
            if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
                throw new CryptographicException("The protected credential is not in the expected format.");

            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(protectedValue[Prefix.Length..]);
            }
            catch (FormatException)
            {
                throw new CryptographicException("The protected credential is not in the expected format.");
            }

            if (payload.Length < NonceSize + TagSize)
                throw new CryptographicException("The protected credential is not in the expected format.");

            var nonce = payload[..NonceSize];
            var tag = payload[NonceSize..(NonceSize + TagSize)];
            var cipherBytes = payload[(NonceSize + TagSize)..];

            var plainBytes = new byte[cipherBytes.Length];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    // Each tenant gets its own key derived from the master key, so a credential
    // encrypted for one company can never be decrypted with another company's key.
    private byte[] DeriveKey(Guid companyId)
        => HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            _masterKey,
            KeySize,
            salt: companyId.ToByteArray(),
            info: Encoding.UTF8.GetBytes("volterp:company-certificate:v1"));
}
