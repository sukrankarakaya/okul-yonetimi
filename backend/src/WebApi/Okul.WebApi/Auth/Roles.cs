namespace Okul.WebApi.Auth;

/// <summary>Faz 1 roles (docs/kapsam.md, docs/domain.md). Code names are English (docs/conventions.md).</summary>
public static class Roles
{
    public const string Administrator = "Administrator"; // Kurucu / Yönetici
    public const string Teacher = "Teacher";             // Öğretmen
    public const string Guardian = "Guardian";           // Veli
    public const string Accountant = "Accountant";       // Muhasebe / İdari personel

    public static readonly IReadOnlyList<string> All = [Administrator, Teacher, Guardian, Accountant];
}

public static class OkulClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string Role = "role";
}
