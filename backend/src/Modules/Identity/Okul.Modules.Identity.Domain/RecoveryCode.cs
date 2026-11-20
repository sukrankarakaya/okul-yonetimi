namespace Okul.Modules.Identity.Domain;

/// <summary>
/// One kurtarma kodu of an account (spec 0004, Requirement 3): only its keyed hash is stored (plan 0004, R3); a used
/// code stays as a record so the remaining count is honest, and never opens a session again.
/// </summary>
public sealed class RecoveryCode
{
    private RecoveryCode()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset? UsedAt { get; private set; }

    public static RecoveryCode Create(Guid id, Guid tenantId, Guid accountId, string codeHash)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty || string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("Kurtarma kodu kaydı eksik.");
        }

        return new RecoveryCode { Id = id, TenantId = tenantId, AccountId = accountId, CodeHash = codeHash };
    }

    public bool IsUnused => UsedAt is null;

    /// <summary>False when it was already used — a code opens a session once.</summary>
    public bool TryUse(DateTimeOffset now)
    {
        if (UsedAt is not null)
        {
            return false;
        }

        UsedAt = now;
        return true;
    }
}
