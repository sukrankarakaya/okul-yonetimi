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
    Task<LoginThrottle?> FindAsync(string key, CancellationToken cancellationToken);

    /// <summary>Insert-or-update in one statement: two concurrent failures for one pair never collide.</summary>
    Task SaveAsync(LoginThrottle throttle, CancellationToken cancellationToken);

    /// <summary>A successful login removes the pair's state (no row is written for a pair that never failed).</summary>
    Task ClearAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Password hashing (plan 0003, R9). Verify with a null hash still does the work of a real check.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string? passwordHash, string password);
}
