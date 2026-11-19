namespace Okul.Modules.Institution.Domain;

/// <summary>
/// Kurum kodu (spec 0003, Requirement 1): 3–12 characters, ASCII letters, digits and '-', unique across kurums.
/// Stored upper-case; compared case-insensitively and culture-independently — <c>i</c>, <c>ı</c>, <c>I</c> and
/// <c>İ</c> all become <c>I</c>, so the Turkish I problem cannot split one code into two (T-1).
/// </summary>
public static class InstitutionCode
{
    public const int MinLength = 3;
    public const int MaxLength = 12;

    /// <summary>The stored form of <paramref name="value"/>, or null when it can never be a kurum kodu.</summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            return null;
        }

        Span<char> normalized = stackalloc char[trimmed.Length];
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i] switch
            {
                'ı' or 'İ' => 'I',
                var other => char.ToUpperInvariant(other),
            };

            if (!(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-'))
            {
                return null;
            }

            normalized[i] = c;
        }

        return new string(normalized);
    }

    /// <summary>Like <see cref="Normalize"/>, for creating a kurum: an invalid code is a programming error.</summary>
    public static string Require(string value) =>
        Normalize(value)
        ?? throw new ArgumentException(
            $"Kurum kodu {MinLength}–{MaxLength} karakter olmalı; yalnızca harf (A–Z), rakam ve tire.", nameof(value));
}
