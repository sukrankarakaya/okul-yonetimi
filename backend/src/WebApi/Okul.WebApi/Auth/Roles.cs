namespace Okul.WebApi.Auth;

/// <summary>Faz 1 roles (docs/kapsam.md, docs/domain.md). Code names are English (docs/conventions.md).</summary>
public static class Roles
{
    public const string Administrator = "Administrator"; // Kurucu / Yönetici
    public const string Teacher = "Teacher";             // Öğretmen
    public const string Guardian = "Guardian";           // Veli
    public const string Accountant = "Accountant";       // Muhasebe / İdari personel

    public static readonly IReadOnlyList<string> All = [Administrator, Teacher, Guardian, Accountant];

    /// <summary>Spec 0007, AC-6: what the fallback policy accepts — a veli session reaches no staff endpoint.</summary>
    public static readonly IReadOnlyList<string> Staff = [Administrator, Teacher, Accountant];
}

public static class OkulClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string Role = "role";

    /// <summary>The session this access belongs to — checked on every request (spec 0003, G5).</summary>
    public const string Session = "sid";

    /// <summary>The session's absolute end (Unix seconds): web 12 hours, mobile 30 days from login.</summary>
    public const string SessionExpiresAt = "sexp";
}
