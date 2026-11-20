namespace Okul.Modules.Identity.Domain;

public enum ClientKind
{
    Web = 1,
    Mobile = 2,
}

public enum RefreshOutcome
{
    /// <summary>The presented refresh credential was current: a new one replaces it.</summary>
    Rotated,

    /// <summary>The one just replaced, within <see cref="Session.ReuseGrace"/>: a parallel refresh — nothing changes.</summary>
    ParallelWithinGrace,

    /// <summary>An already used credential (theft signal, Requirement 5): the session is closed.</summary>
    Reused,

    /// <summary>The absolute limit passed or the session was closed: the user logs in again.</summary>
    Ended,
}

/// <summary>
/// One login on one device (spec 0003, Requirements 5–7). Access lasts 15 minutes and is renewed silently with a
/// refresh credential that rotates on every use; the session has a fixed absolute limit from login (web 12 hours,
/// mobile 30 days) that use never extends. Closing (logout, password change) takes effect on the next request (G5).
/// Only hashes of refresh credentials are kept.
/// </summary>
public sealed class Session
{
    public static readonly TimeSpan WebLifetime = TimeSpan.FromHours(12);
    public static readonly TimeSpan MobileLifetime = TimeSpan.FromDays(30);

    /// <summary>Two tabs refreshing at once must not look like theft (plan 0003, R5).</summary>
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(10);

    private Session()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public ClientKind Client { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public string CurrentRefreshHash { get; private set; } = string.Empty;

    public string? PreviousRefreshHash { get; private set; }

    public DateTimeOffset? RotatedAt { get; private set; }

    public static Session Start(
        Guid id, Guid tenantId, Guid accountId, ClientKind client, DateTimeOffset now, string refreshHash)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || accountId == Guid.Empty)
        {
            throw new ArgumentException("Oturum, kurum ve hesap kimliği boş olamaz.");
        }

        if (!Enum.IsDefined(client))
        {
            throw new ArgumentOutOfRangeException(nameof(client));
        }

        return new Session
        {
            Id = id,
            TenantId = tenantId,
            AccountId = accountId,
            Client = client,
            StartedAt = now,
            ExpiresAt = now + (client == ClientKind.Web ? WebLifetime : MobileLifetime),
            CurrentRefreshHash = RequireHash(refreshHash),
        };
    }

    public bool IsActive(DateTimeOffset now) => ClosedAt is null && now < ExpiresAt;

    public RefreshOutcome Refresh(string presentedHash, string newHash, DateTimeOffset now)
    {
        if (!IsActive(now))
        {
            return RefreshOutcome.Ended;
        }

        if (string.Equals(presentedHash, CurrentRefreshHash, StringComparison.Ordinal))
        {
            PreviousRefreshHash = CurrentRefreshHash;
            CurrentRefreshHash = RequireHash(newHash);
            RotatedAt = now;
            return RefreshOutcome.Rotated;
        }

        if (PreviousRefreshHash is not null
            && string.Equals(presentedHash, PreviousRefreshHash, StringComparison.Ordinal)
            && RotatedAt is { } rotated
            && now - rotated <= ReuseGrace)
        {
            return RefreshOutcome.ParallelWithinGrace;
        }

        Close(now);
        return RefreshOutcome.Reused;
    }

    public void Close(DateTimeOffset now) => ClosedAt ??= now;

    private static string RequireHash(string hash) =>
        string.IsNullOrWhiteSpace(hash) ? throw new ArgumentException("Yenileme özeti boş olamaz.", nameof(hash)) : hash;
}
