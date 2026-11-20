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

    /// <summary>The authenticator secret, protected (spec 0004, Requirement 10); null while 2FA is off.</summary>
    public string? TwoFactorSecret { get; private set; }

    /// <summary>The last accepted 30-second step: a code is accepted only for a later step (Requirement 5; plan 0004, R6).</summary>
    public long TwoFactorLastStep { get; private set; }

    public bool TwoFactorEnabled => TwoFactorSecret is not null;

    /// <summary>Mandatory when any role requires it (Requirement 1).</summary>
    public bool TwoFactorRequired => StaffRoles.RequireTwoFactor(_roles);

    /// <summary>Only a Teacher-only account may turn 2FA off (Requirement 7).</summary>
    public bool CanDisableTwoFactor => TwoFactorEnabled && !TwoFactorRequired;

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

    /// <summary>A confirmed setup: the secret and the step of the code that confirmed it (that code is now used).</summary>
    public void EnableTwoFactor(string protectedSecret, long confirmedStep)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            throw new ArgumentException("Anahtar boş olamaz.", nameof(protectedSecret));
        }

        TwoFactorSecret = protectedSecret;
        TwoFactorLastStep = confirmedStep;
    }

    /// <summary>
    /// Accepts a code's step only when it is later than the last accepted one: the same code — or an older one — is
    /// refused a second time (Requirement 5). False when refused.
    /// </summary>
    public bool TryAcceptStep(long step)
    {
        if (!TwoFactorEnabled || step <= TwoFactorLastStep)
        {
            return false;
        }

        TwoFactorLastStep = step;
        return true;
    }

    /// <summary>Teacher turning it off (Requirement 7) or the reset command (Requirement 9).</summary>
    public void ClearTwoFactor()
    {
        TwoFactorSecret = null;
        TwoFactorLastStep = 0;
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
