namespace Okul.Modules.Identity.Domain;

/// <summary>
/// Faz 1 roles (docs/kapsam.md). The names match the API's role claims. Guardian cannot log in with e-mail and
/// password (spec 0003 — veli girişi is spec 0005); an account must hold at least one staff role.
/// </summary>
public static class StaffRoles
{
    public const string Administrator = "Administrator";
    public const string Teacher = "Teacher";
    public const string Accountant = "Accountant";
    public const string Guardian = "Guardian";

    public static readonly IReadOnlySet<string> Staff = new HashSet<string>(StringComparer.Ordinal)
    {
        Administrator, Teacher, Accountant,
    };

    /// <summary>Roles for which two-step verification is mandatory (spec 0004, Requirement 1; docs/security.md).</summary>
    public static readonly IReadOnlySet<string> TwoFactorMandatory = new HashSet<string>(StringComparer.Ordinal)
    {
        Administrator, Accountant,
    };

    /// <summary>One mandatory role is enough — a Teacher + Accountant account must use it too.</summary>
    public static bool RequireTwoFactor(IEnumerable<string> roles) => roles.Any(TwoFactorMandatory.Contains);

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Administrator, Teacher, Accountant, Guardian,
    };
}
