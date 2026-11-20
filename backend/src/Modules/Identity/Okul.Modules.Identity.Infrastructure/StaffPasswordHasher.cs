using Microsoft.AspNetCore.Identity;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Infrastructure;

/// <summary>
/// ASP.NET Core's <see cref="PasswordHasher{TUser}"/> (PBKDF2, salted, versioned) and nothing else of ASP.NET Core
/// Identity (plan 0003, R9). For an unknown account the check runs against a fixed dummy hash, so the time taken does
/// not tell a wrong password from a missing account (Requirement 3).
/// </summary>
internal sealed class StaffPasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<StaffAccount> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(null!, "okul-yok-hesap-" + Guid.NewGuid());

    public string Hash(string password) => Hasher.HashPassword(null!, password);

    public bool Verify(string? passwordHash, string password)
    {
        var result = Hasher.VerifyHashedPassword(null!, passwordHash ?? DummyHash, password);
        return passwordHash is not null && result != PasswordVerificationResult.Failed;
    }
}
