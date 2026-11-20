namespace Okul.Modules.Institution.Contracts;

/// <summary>
/// THE normalization of a kurum kodu (spec 0003, Requirement 1) — public so every module compares codes the same way:
/// 3–12 characters, ASCII letters, digits and '-', upper-case; <c>i</c>, <c>ı</c>, <c>I</c> and <c>İ</c> all become
/// <c>I</c> (T-1). <c>ToUpperInvariant</c> alone is not enough: it leaves <c>İ</c> as <c>İ</c> (spec 0003 review round 1 #3).
/// </summary>
public static class InstitutionCodeFormat
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
}
