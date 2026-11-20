using System.Security.Cryptography;

namespace Okul.Modules.Identity.Domain;

/// <summary>
/// Kurtarma kodları (spec 0004, Requirement 3): 10 single-use codes, <c>XXXX-XXXX</c>, from an alphabet without the
/// look-alikes 0/O and 1/I/L. Input ignores case, dashes and white space; anything else is no code.
/// </summary>
public static class RecoveryCodeFormat
{
    public const int CodesPerSet = 10;
    public const int Length = 8;
    public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static IReadOnlyList<string> NewSet()
    {
        var codes = new List<string>(CodesPerSet);
        while (codes.Count < CodesPerSet)
        {
            var raw = RandomNumberGenerator.GetString(Alphabet, Length);
            var code = raw[..4] + "-" + raw[4..];
            if (!codes.Contains(code, StringComparer.Ordinal))
            {
                codes.Add(code);
            }
        }

        return codes;
    }

    /// <summary>The 8 alphabet characters of a code (the form that is hashed), or null.</summary>
    public static string? Normalize(string? input)
    {
        if (input is null)
        {
            return null;
        }

        var chars = input.Where(c => c != '-' && !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray();
        return chars.Length == Length && chars.All(Alphabet.Contains) ? new string(chars) : null;
    }
}
