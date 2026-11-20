namespace Okul.Modules.Identity.Domain;

/// <summary>
/// The stored, compared form of a staff e-mail (spec 0003, Requirement 1): trimmed, lower-case, culture-independent.
/// <c>İ</c>, <c>I</c> and <c>ı</c> all become <c>i</c> (T-1), so "Ali@…" and "alİ@…" are one account.
/// </summary>
public static class EmailAddress
{
    public const int MaxLength = 254;

    /// <summary>The normalized address, or null when <paramref name="value"/> cannot be an e-mail address.</summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength)
        {
            return null;
        }

        var at = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1 || trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }

        return string.Create(trimmed.Length, trimmed, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = source[i] switch
                {
                    'İ' or 'I' or 'ı' => 'i',
                    var c => char.ToLowerInvariant(c),
                };
            }
        });
    }

    public static string Require(string value) =>
        Normalize(value) ?? throw new ArgumentException("Geçerli bir e-posta adresi değil.", nameof(value));
}
