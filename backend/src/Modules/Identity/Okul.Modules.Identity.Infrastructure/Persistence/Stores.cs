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

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }
}

internal sealed class LoginChallengeStore : ILoginChallengeStore
{
    private readonly IdentityDbContext _db;

    public LoginChallengeStore(IdentityDbContext db)
    {
        _db = db;
    }

    public Task<LoginChallenge?> FindAsync(Guid challengeId, CancellationToken cancellationToken) =>
        _db.LoginChallenges.SingleOrDefaultAsync(c => c.Id == challengeId, cancellationToken);

    public async Task AddAsync(LoginChallenge challenge, CancellationToken cancellationToken) =>
        await _db.LoginChallenges.AddAsync(challenge, cancellationToken);

    public Task EndAllAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken) =>
        _db.LoginChallenges
            .Where(c => c.AccountId == accountId && c.EndedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.EndedAt, now), cancellationToken);
}

internal sealed class RecoveryCodeStore : IRecoveryCodeStore
{
    private readonly IdentityDbContext _db;

    public RecoveryCodeStore(IdentityDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RecoveryCode>> ListUnusedAsync(Guid accountId, CancellationToken cancellationToken) =>
        await _db.RecoveryCodes.Where(c => c.AccountId == accountId && c.UsedAt == null).ToListAsync(cancellationToken);

    public Task<int> CountUnusedAsync(Guid accountId, CancellationToken cancellationToken) =>
        _db.RecoveryCodes.CountAsync(c => c.AccountId == accountId && c.UsedAt == null, cancellationToken);

    /// <summary>The delete runs now; the new codes are written with the caller's SaveChanges (one unit with the account).</summary>
    public async Task ReplaceAsync(Guid tenantId, Guid accountId, IEnumerable<string> codeHashes, CancellationToken cancellationToken)
    {
        await DeleteAllAsync(accountId, cancellationToken);
        foreach (var hash in codeHashes)
        {
            await _db.RecoveryCodes.AddAsync(RecoveryCode.Create(Guid.CreateVersion7(), tenantId, accountId, hash), cancellationToken);
        }
    }

    public Task DeleteAllAsync(Guid accountId, CancellationToken cancellationToken) =>
        _db.RecoveryCodes.Where(c => c.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
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

    public async Task<bool> CloseAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await _db.Sessions
            .Where(s => s.Id == sessionId && s.ClosedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.ClosedAt, now), cancellationToken) > 0;

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

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
    /// One transaction holding a PostgreSQL advisory lock on the key: concurrent attempts for one pair run one after the
    /// other, so no failure is lost and a lock that lands during a success is seen (review round 1 #1). The lock is
    /// released at commit or rollback. The first attempt of a new pair inserts under the same lock — no INSERT race
    /// (23505) either. The rules stay in <see cref="LoginThrottle"/>.
    /// </summary>
    public async Task<T> UpdateAsync<T>(string key, Func<LoginThrottle, T> change, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

        var throttle = await _db.LoginThrottles.SingleOrDefaultAsync(t => t.Key == key, cancellationToken);
        if (throttle is null)
        {
            throttle = LoginThrottle.Create(key);
            _db.LoginThrottles.Add(throttle);
        }

        var result = change(throttle);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
