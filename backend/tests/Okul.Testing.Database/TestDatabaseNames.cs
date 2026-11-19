using System.Globalization;

namespace Okul.Testing.Database;

/// <summary>
/// Names of the per-run test databases: <c>okul_test_&lt;yyyyMMddHHmmss UTC&gt;_&lt;8 hex&gt;</c> (spec 0002, CB-2).
/// The creation time lives in the name because PostgreSQL does not record when a database was created;
/// the sweep (CB-3) reads it back. Pure — the caller passes "now".
/// </summary>
public static class TestDatabaseNames
{
    public const string Prefix = "okul_test_";

    private const string TimeFormat = "yyyyMMddHHmmss";
    private const int SuffixLength = 8;

    /// <summary>A database whose name time is more than this far in the past belongs to a dead run.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(60);

    public static string NewName(DateTimeOffset now) =>
        Prefix
        + now.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture)
        + "_"
        + Guid.NewGuid().ToString("N")[..SuffixLength];

    /// <summary>The UTC creation time written into <paramref name="name"/>, or null when the name is not ours or unreadable.</summary>
    public static DateTimeOffset? CreatedAt(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = name[Prefix.Length..].Split('_');
        if (parts.Length != 2 || parts[1].Length != SuffixLength || !parts[1].All(Uri.IsHexDigit))
        {
            return null;
        }

        return DateTime.TryParseExact(
            parts[0],
            TimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var created)
            ? new DateTimeOffset(created, TimeSpan.Zero)
            : null;
    }

    /// <summary>True only for our own, readable names older than <see cref="StaleAfter"/>; everything else is left alone.</summary>
    public static bool IsStale(string name, DateTimeOffset now) =>
        CreatedAt(name) is { } created && now.ToUniversalTime() - created > StaleAfter;
}
