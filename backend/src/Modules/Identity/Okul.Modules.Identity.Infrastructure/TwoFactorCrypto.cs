using System.Security.Cryptography;
using System.Text;
using Okul.Modules.Identity.Application;

namespace Okul.Modules.Identity.Infrastructure;

/// <summary>
/// The 2FA key (<c>Auth:TwoFactorKey</c>, 32 bytes, plan 0004 R2) — kept in configuration like <c>Auth:SigningKey</c>,
/// never in the database: a database copy alone neither decrypts a secret nor tests a recovery code.
/// </summary>
public sealed class TwoFactorKey
{
    public const int Length = 32;

    public TwoFactorKey(byte[] key)
    {
        if (key.Length != Length)
        {
            throw new InvalidOperationException("Auth:TwoFactorKey must be exactly 32 bytes.");
        }

        Key = key;
    }

    internal byte[] Key { get; }

    internal byte[] Derive(string purpose) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Key, Length, info: Encoding.UTF8.GetBytes(purpose));
}

/// <summary>
/// AES-GCM. Format <c>v1:&lt;base64(nonce | tag | ciphertext)&gt;</c>; the version prefix leaves room for a key rotation
/// (plan 0004, R2). Tampered or foreign data does not decrypt — it throws.
/// </summary>
internal sealed class TwoFactorSecretProtector : ITwoFactorSecretProtector
{
    private const string Version = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public TwoFactorSecretProtector(TwoFactorKey key)
    {
        _key = key.Derive("okul/2fa/secret/v1");
    }

    public string Protect(ReadOnlySpan<byte> secret)
    {
        var output = new byte[NonceSize + TagSize + secret.Length];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, secret, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return Version + Convert.ToBase64String(output);
    }

    public byte[] Unprotect(string protectedSecret)
    {
        if (!protectedSecret.StartsWith(Version, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unknown 2FA secret format.");
        }

        var data = Convert.FromBase64String(protectedSecret[Version.Length..]);
        if (data.Length <= NonceSize + TagSize)
        {
            throw new CryptographicException("Truncated 2FA secret.");
        }

        var plain = new byte[data.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(data.AsSpan(0, NonceSize), data.AsSpan(NonceSize + TagSize), data.AsSpan(NonceSize, TagSize), plain);
        return plain;
    }
}

/// <summary>HMAC-SHA256 with a key derived from <see cref="TwoFactorKey"/> (plan 0004, R3).</summary>
internal sealed class RecoveryCodeHasher : IRecoveryCodeHasher
{
    private readonly byte[] _key;

    public RecoveryCodeHasher(TwoFactorKey key)
    {
        _key = key.Derive("okul/2fa/recovery/v1");
    }

    public string Hash(string normalizedCode) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalizedCode)));
}
