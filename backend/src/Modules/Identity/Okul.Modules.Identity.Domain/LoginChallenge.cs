namespace Okul.Modules.Identity.Domain;

public enum LoginChallengePurpose
{
    /// <summary>2FA is on: the code (or a recovery code) completes the login.</summary>
    Verify,

    /// <summary>2FA is required but not set up: the setup must be confirmed first (Requirement 2).</summary>
    Setup,
}

/// <summary>
/// The step between a correct password and a session (spec 0004, Requirements 2 and 4; plan 0004, R1). It is not a
/// session: it opens no data. Valid for 5 minutes from the password step; success, "Vazgeç" or expiry end it. A setup
/// keeps the not-yet-confirmed secret here (protected) — an abandoned setup is never stored on the account.
/// </summary>
public sealed class LoginChallenge
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private LoginChallenge()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public LoginChallengePurpose Purpose { get; private set; }

    public ClientKind Client { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Hash of the opaque credential the client holds — never the credential itself.</summary>
    public string CredentialHash { get; private set; } = string.Empty;

    /// <summary>Setup only: the secret shown in the QR code, protected; null until the setup step asks for it.</summary>
    public string? PendingProtectedSecret { get; private set; }

    public static LoginChallenge Start(
        Guid id, Guid tenantId, Guid accountId, LoginChallengePurpose purpose, ClientKind client, DateTimeOffset now,
        string credentialHash)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new ArgumentException("Kimlikler boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(credentialHash))
        {
            throw new ArgumentException("Özet boş olamaz.", nameof(credentialHash));
        }

        return new LoginChallenge
        {
            Id = id,
            TenantId = tenantId,
            AccountId = accountId,
            Purpose = purpose,
            Client = client,
            ExpiresAt = now + Lifetime,
            CredentialHash = credentialHash,
        };
    }

    public bool IsUsable(DateTimeOffset now) => EndedAt is null && now < ExpiresAt;

    /// <summary>A new setup attempt replaces any earlier pending secret — the QR code always matches the last secret.</summary>
    public void SetPendingSecret(string protectedSecret)
    {
        if (Purpose != LoginChallengePurpose.Setup)
        {
            throw new InvalidOperationException("Yalnızca kurulum adımı anahtar taşır.");
        }

        PendingProtectedSecret = protectedSecret;
    }

    /// <summary>Success or "Vazgeç": the step can never be used again.</summary>
    public void End(DateTimeOffset now) => EndedAt ??= now;
}
