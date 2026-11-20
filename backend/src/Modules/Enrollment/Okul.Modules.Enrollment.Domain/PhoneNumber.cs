namespace Okul.Modules.Enrollment.Domain;

/// <summary>
/// A Türkiye cep telefonu (spec 0007, Requirement 1): <c>0532…</c>, <c>532…</c>, <c>+90 532…</c> and <c>0090 532…</c> are
/// the same number; spaces, dashes, dots and parentheses are ignored. Anything else — a landline, a foreign number, a
/// letter — is no number. Kept as E.164 (<c>+905321234567</c>); shown only masked (Requirement 11).
/// </summary>
public sealed record PhoneNumber
{
    private PhoneNumber(string national) => National = national;

    /// <summary>The 10 national digits, starting with 5.</summary>
    public string National { get; }

    public string E164 => "+90" + National;

    /// <summary>Only the last two digits open: <c>+90 5** *** ** 67</c> (spec 0006, Requirement 7; T-10).</summary>
    public string Masked => $"+90 {National[0]}** *** ** {National[^2..]}";

    public override string ToString() => E164;

    public static PhoneNumber? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 32)
        {
            return null;
        }

        Span<char> digits = stackalloc char[input.Length];
        var count = 0;
        var plus = false;
        foreach (var c in input)
        {
            if (char.IsAsciiDigit(c))
            {
                digits[count++] = c;
            }
            else if (c is ' ' or '-' or '.' or '(' or ')')
            {
                continue;
            }
            else if (c == '+' && count == 0 && !plus)
            {
                plus = true;
            }
            else
            {
                return null;
            }
        }

        var number = new string(digits[..count]);
        if (plus)
        {
            if (!number.StartsWith("90", StringComparison.Ordinal))
            {
                return null;
            }

            number = number[2..];
        }
        else if (number.Length == 14 && number.StartsWith("0090", StringComparison.Ordinal))
        {
            number = number[4..];
        }
        else if (number.Length == 12 && number.StartsWith("90", StringComparison.Ordinal))
        {
            number = number[2..];
        }
        else if (number.Length == 11 && number[0] == '0')
        {
            number = number[1..];
        }

        return number.Length == 10 && number[0] == '5' ? new PhoneNumber(number) : null;
    }

    /// <summary>A stored E.164 value back to a number (persistence).</summary>
    public static PhoneNumber FromE164(string e164) =>
        Parse(e164) ?? throw new ArgumentException("Geçerli bir E.164 cep telefonu değil.", nameof(e164));
}
