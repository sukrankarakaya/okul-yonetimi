using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

/// <summary>Staff accounts of the CURRENT tenant (tenant filter — from the session or an <c>ITenantScope</c>).</summary>
public interface IStaffAccountStore
{
    Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<StaffAccount?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task AddAsync(StaffAccount account, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Sessions of the CURRENT tenant.</summary>
public interface ISessionStore
{
    Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken);

    Task AddAsync(Session session, CancellationToken cancellationToken);

    /// <summary>Closes every open session of the account except <paramref name="keepSessionId"/> (Requirement 7).</summary>
    Task CloseOthersAsync(Guid accountId, Guid keepSessionId, DateTimeOffset now, CancellationToken cancellationToken);

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
