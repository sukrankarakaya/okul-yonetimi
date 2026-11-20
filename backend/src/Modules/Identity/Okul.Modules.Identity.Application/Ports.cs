using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

/// <summary>Staff accounts of the CURRENT tenant (tenant filter — from the session or an <c>ITenantScope</c>).</summary>
public interface IStaffAccountStore
{
    Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<StaffAccount?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task AddAsync(StaffAccount account, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves, unless the account's last accepted 2FA step changed since it was read (spec 0004, plan R6): false then, and
    /// nothing is written — two parallel requests cannot both accept one code.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Sessions of the CURRENT tenant.</summary>
public interface ISessionStore
{
    Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken);

    Task AddAsync(Session session, CancellationToken cancellationToken);

    /// <summary>Closes every open session of the account except <paramref name="keepSessionId"/> (Requirement 7).</summary>
    Task CloseOthersAsync(Guid accountId, Guid keepSessionId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Closes every open session of the account (a 2FA reset — spec 0004, Requirement 9); the number closed.</summary>
    Task<int> CloseAllAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Closes one session in a single UPDATE (if still open). False when there is no such open session.</summary>
    Task<bool> CloseAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Saves, unless the refresh credential changed since it was read (optimistic concurrency on the current hash):
    /// false then, and nothing is written.
    /// </summary>
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Failed-login state by kurum kodu + e-posta key — not tenant-scoped by design (plan 0003, R4).</summary>
public interface ILoginThrottleStore
{
    /// <summary>A cheap, unlocked read — only to refuse early while locked.</summary>
    Task<LoginThrottle?> FindAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Read-modify-write of one pair's state, serialized per key (spec 0003 review round 1 #1: concurrent failures lost
    /// counts). The state is created when missing; <paramref name="change"/> runs on the latest state and is persisted.
    /// </summary>
    Task<T> UpdateAsync<T>(string key, Func<LoginThrottle, T> change, CancellationToken cancellationToken);
}

/// <summary>Password hashing (plan 0003, R9). Verify with a null hash still does the work of a real check.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string? passwordHash, string password);
}

/// <summary>The steps between a correct password and a session (spec 0004; plan 0004, R1), of the CURRENT tenant.</summary>
public interface ILoginChallengeStore
{
    Task<LoginChallenge?> FindAsync(Guid challengeId, CancellationToken cancellationToken);

    Task AddAsync(LoginChallenge challenge, CancellationToken cancellationToken);

    /// <summary>Ends every open step of the account (a 2FA reset — Requirement 9).</summary>
    Task EndAllAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Kurtarma kodları of the CURRENT tenant — keyed hashes only (plan 0004, R3).</summary>
public interface IRecoveryCodeStore
{
    Task<IReadOnlyList<RecoveryCode>> ListUnusedAsync(Guid accountId, CancellationToken cancellationToken);

    Task<int> CountUnusedAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Removes every code of the account (used or not) and adds the new hashes — old codes stop at once.</summary>
    Task ReplaceAsync(Guid tenantId, Guid accountId, IEnumerable<string> codeHashes, CancellationToken cancellationToken);

    Task DeleteAllAsync(Guid accountId, CancellationToken cancellationToken);
}

/// <summary>Encrypts the authenticator secret at rest (spec 0004, Requirement 10; plan 0004, R2).</summary>
public interface ITwoFactorSecretProtector
{
    string Protect(ReadOnlySpan<byte> secret);

    byte[] Unprotect(string protectedSecret);
}

/// <summary>Keyed hash of a normalized kurtarma kodu (plan 0004, R3): the database alone cannot be brute-forced.</summary>
public interface IRecoveryCodeHasher
{
    string Hash(string normalizedCode);
}
