namespace Okul.Modules.Identity.Domain;

/// <summary>Spec 0003, Requirement 7: a new password has at least 10 characters.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    public static bool IsAcceptable(string? password) => password is not null && password.Length >= MinLength;
}
