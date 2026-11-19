using Microsoft.EntityFrameworkCore;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Infrastructure.Persistence;

/// <summary>Tenant filtering comes from <see cref="IdentityDbContext"/> — never bypassed here (T-2).</summary>
internal sealed class StaffAccountStore : IStaffAccountStore
{
    private readonly IdentityDbContext _db;

    public StaffAccountStore(IdentityDbContext db)
    {
        _db = db;
    }

    public Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        _db.StaffAccounts.SingleOrDefaultAsync(a => a.Email == normalizedEmail, cancellationToken);

    public Task<StaffAccount?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        _db.StaffAccounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);

    public async Task AddAsync(StaffAccount account, CancellationToken cancellationToken) =>
        await _db.StaffAccounts.AddAsync(account, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}

internal sealed class SessionStore : ISessionStore
{
    private readonly IdentityDbContext _db;

    public SessionStore(IdentityDbContext db)
    {
        _db = db;
    }

    public Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken) =>
        _db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task AddAsync(Session session, CancellationToken cancellationToken) =>
        await _db.Sessions.AddAsync(session, cancellationToken);

    /// <summary>One UPDATE under the tenant filter (bulk updates honour query filters).</summary>
    public Task CloseOthersAsync(Guid accountId, Guid keepSessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.Sessions
            .Where(s => s.AccountId == accountId && s.Id != keepSessionId && s.ClosedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.ClosedAt, now), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}

internal sealed class LoginThrottleStore : ILoginThrottleStore
{
    private readonly LoginThrottleDbContext _db;

    public LoginThrottleStore(LoginThrottleDbContext db)
    {
        _db = db;
    }

    public Task<LoginThrottle?> FindAsync(string key, CancellationToken cancellationToken) =>
        _db.LoginThrottles.AsNoTracking().SingleOrDefaultAsync(t => t.Key == key, cancellationToken);

    /// <summary>
    /// One upsert (spec 0003 build: two concurrent logins for one pair raced on INSERT → 23505 → 500). Concurrent
    /// failures may lose a count to last-write-wins; the lock still comes, at most one attempt later.
    /// </summary>
    public Task SaveAsync(LoginThrottle throttle, CancellationToken cancellationToken) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO kimlik.login_throttles (key, failure_count, last_failure_at, locked_until)
            VALUES ({throttle.Key}, {throttle.FailureCount}, {throttle.LastFailureAt}, {throttle.LockedUntil})
            ON CONFLICT (key) DO UPDATE SET
                failure_count = EXCLUDED.failure_count,
                last_failure_at = EXCLUDED.last_failure_at,
                locked_until = EXCLUDED.locked_until
            """,
            cancellationToken);

    public Task ClearAsync(string key, CancellationToken cancellationToken) =>
        _db.LoginThrottles.Where(t => t.Key == key).ExecuteDeleteAsync(cancellationToken);
}
