using System.Security.Cryptography;
using System.Text;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Domain;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Enrollment.Infrastructure;

/// <summary>
/// The secret behind the veli login hashes (<c>Auth:GuardianCodeKey</c>, 32 bytes, base64; user-secrets / secret manager —
/// docs/security.md). Without it a database copy would let anyone try all 10-digit telefonlar and 6-digit codes.
/// </summary>
public sealed class GuardianLoginKey
{
    public const int Length = 32;

    public GuardianLoginKey(byte[] key)
    {
        if (key.Length != Length)
        {
            throw new InvalidOperationException("Auth:GuardianCodeKey must be exactly 32 bytes.");
        }

        Key = key;
    }

    internal byte[] Key { get; }

    internal byte[] Derive(string purpose) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, Key, Length, info: Encoding.UTF8.GetBytes(purpose));
}

/// <summary>HMAC-SHA256 pair keys and code hashes (spec 0007, Requirement 11; AC-8).</summary>
public sealed class GuardianCodeHasher : IGuardianLoginHasher
{
    private readonly byte[] _pairKey;
    private readonly byte[] _codeKey;

    public GuardianCodeHasher(GuardianLoginKey key)
    {
        _pairKey = key.Derive("okul/kayit/guardian-login/pair/v1");
        _codeKey = key.Derive("okul/kayit/guardian-login/code/v1");
    }

    public string PairKey(Guid? tenantId, string? institutionCode, PhoneNumber phone)
    {
        ArgumentNullException.ThrowIfNull(phone);

        // As Kimlik's login key (spec 0003 review round 1 #3): every spelling of a known code shares one key, and an unknown
        // code goes through the same normalization — the difference must not tell whether a kurum kodu exists.
        var kurum = tenantId?.ToString("N")
            ?? "kod:" + (InstitutionCodeFormat.Normalize(institutionCode) ?? institutionCode?.Trim().ToUpperInvariant() ?? string.Empty);
        return Hash(_pairKey, kurum + "\n" + phone.E164);
    }

    public string CodeHash(string pairKey, string code) => Hash(_codeKey, pairKey + "\n" + code);

    private static string Hash(byte[] key, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
}
