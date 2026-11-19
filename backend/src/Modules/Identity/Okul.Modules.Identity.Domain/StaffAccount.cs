namespace Okul.Modules.Identity.Domain;

/// <summary>
/// A staff member's login account (spec 0003). One account belongs to exactly one kurum (tenant); the same e-mail
/// may exist in two kurums as two accounts. Holds only the password hash — the password itself never.
/// </summary>
public sealed class StaffAccount
{
    private readonly List<string> _roles = [];

    private StaffAccount()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public IReadOnlyList<string> Roles => _roles;

    /// <summary>Only an account with a staff role may use staff login; a guardian-only account may not (AC-2).</summary>
    public bool CanLogInAsStaff => _roles.Any(StaffRoles.Staff.Contains);

    public static StaffAccount Create(Guid id, Guid tenantId, string email, string passwordHash, IEnumerable<string> roles)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new ArgumentException("Hesap ve kurum kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Şifre özeti boş olamaz.", nameof(passwordHash));
        }

        var account = new StaffAccount
        {
            Id = id,
            TenantId = tenantId,
            Email = EmailAddress.Require(email),
            PasswordHash = passwordHash,
        };

        foreach (var role in roles.Distinct(StringComparer.Ordinal))
        {
            if (!StaffRoles.All.Contains(role))
            {
                throw new ArgumentException($"Bilinmeyen rol: {role}", nameof(roles));
            }

            account._roles.Add(role);
        }

        if (account._roles.Count == 0)
        {
            throw new ArgumentException("Hesabın en az bir rolü olmalı.", nameof(roles));
        }

        return account;
    }

    public void ChangePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new ArgumentException("Şifre özeti boş olamaz.", nameof(newPasswordHash));
        }

        PasswordHash = newPasswordHash;
    }
}
