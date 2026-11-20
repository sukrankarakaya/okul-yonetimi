using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Okul.Modules.Identity.Domain;

/// <summary>
/// Time-based one-time passwords (RFC 6238 over RFC 4226): 6 digits, 30-second steps, HMAC-SHA1 — what common
/// authenticator apps produce (spec 0004, Requirement 5). Pure; the clock is the caller's.
/// </summary>
public static class Totp
{
    public const int Digits = 6;
    public const int SecretLength = 20;
    public static readonly TimeSpan StepLength = TimeSpan.FromSeconds(30);

    /// <summary>The step a moment falls in: whole 30-second periods since the Unix epoch.</summary>
    public static long StepAt(DateTimeOffset moment) => moment.ToUnixTimeSeconds() / (long)StepLength.TotalSeconds;

    public static string Compute(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7FFFFFFF) % 1_000_000;
        return binary.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The step whose code equals <paramref name="code"/> among the previous, current and next step of
    /// <paramref name="now"/> (device clock drift), or null. Spaces in the code are ignored. Compared in constant time.
    /// </summary>
    public static long? MatchingStep(ReadOnlySpan<byte> secret, string? code, DateTimeOffset now)
    {
        var digits = Normalize(code);
        if (digits is null)
        {
            return null;
        }

        var current = StepAt(now);
        long? match = null;
        for (var step = current - 1; step <= current + 1; step++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Compute(secret, step)), Encoding.ASCII.GetBytes(digits)))
            {
                match ??= step;
            }
        }

        return match;
    }

    /// <summary>Six ASCII digits after removing white space; anything else is no code at all.</summary>
    public static string? Normalize(string? code)
    {
        if (code is null)
        {
            return null;
        }

        var digits = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return digits.Length == Digits && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretLength);

    /// <summary>RFC 4648 base32 without padding — the "anahtar" a user types into an authenticator app.</summary>
    public static string ToBase32(ReadOnlySpan<byte> data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            output.Append(alphabet[(buffer << (5 - bits)) & 31]);
        }

        return output.ToString();
    }

    /// <summary>The otpauth:// URI an authenticator app reads from the QR code (issuer + account label).</summary>
    public static string ProvisioningUri(string issuer, string accountLabel, ReadOnlySpan<byte> secret)
    {
        var label = Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(accountLabel);
        return $"otpauth://totp/{label}?secret={ToBase32(secret)}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={(int)StepLength.TotalSeconds}";
    }
}
