using System.Security.Cryptography;
using System.Text;

namespace Okul.Modules.Enrollment.Domain;

public enum CodeCheck
{
    Accepted,

    /// <summary>Wrong, already used, replaced by a newer code or cancelled — one answer for all (spec 0007, AC-2).</summary>
    Wrong,

    /// <summary>The 5 minutes are over: "Kodun süresi doldu. Yeni kod isteyin."</summary>
    Expired,
}

/// <summary>
/// One SMS login code for one kurum kodu + telefon pair (spec 0007, Requirement 3; K2): 6 random digits, single use,
/// valid 5 minutes; a newer code, "Vazgeç" or a use ends it. Only a keyed hash is kept (Requirement 11).
/// <para>
/// A pair that is NOT a registered veli gets a code too — never sent, its hash random — so that expiry, "Vazgeç" and
/// wrong-code answers are the same whether the number is registered or not (K3, G-3): nothing tells them apart.
/// Keyed by the pair, not by a kurum: an unknown kurum kodu behaves the same way.
/// </para>
/// </summary>
public sealed class GuardianLoginCode
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private GuardianLoginCode()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Keyed hash of the normalized kurum kodu + telefon pair (as <see cref="GuardianLoginThrottle.Key"/>).</summary>
    public string PairKey { get; private set; } = string.Empty;

    /// <summary>The registered veli the code was sent to; null when the pair is not one (the code was never sent).</summary>
    public Guid? GuardianId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Used, replaced by a newer code, or cancelled with "Vazgeç".</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    public static GuardianLoginCode Issue(Guid id, string pairKey, Guid? guardianId, string codeHash, DateTimeOffset now)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(pairKey) || string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("Kod kimliği, çift anahtarı ve kod özeti boş olamaz.");
        }

        return new GuardianLoginCode
        {
            Id = id,
            PairKey = pairKey,
            GuardianId = guardianId,
            CodeHash = codeHash,
            IssuedAt = now,
            ExpiresAt = now + Lifetime,
        };
    }

    public bool IsPending(DateTimeOffset now) => EndedAt is null && now < ExpiresAt;

    /// <summary>Checks a presented code hash; an accepted code is used up at once.</summary>
    public CodeCheck Check(string presentedHash, DateTimeOffset now)
    {
        if (EndedAt is not null)
        {
            return CodeCheck.Wrong;
        }

        if (now >= ExpiresAt)
        {
            return CodeCheck.Expired;
        }

        if (GuardianId is null || !FixedTimeEquals(presentedHash, CodeHash))
        {
            return CodeCheck.Wrong;
        }

        EndedAt = now;
        return CodeCheck.Accepted;
    }

    /// <summary>A newer code or "Vazgeç" (G-1) ends this one.</summary>
    public void End(DateTimeOffset now) => EndedAt ??= now;

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a ?? string.Empty), Encoding.UTF8.GetBytes(b));
}

/// <summary>What a veli types: 6 digits; white space is ignored (spec 0007, Requirement 3).</summary>
public static class GuardianLoginCodeFormat
{
    public const int Length = 6;

    public static string New() => RandomNumberGenerator.GetString("0123456789", Length);

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var compact = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
        return compact.Length == Length && compact.All(char.IsAsciiDigit) ? compact : null;
    }
}
