using Okul.Core.Tenancy;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Identity.UnitTests.Application;

/// <summary>In-memory ports that honour the tenant scope like the real tenant filter does.</summary>
internal sealed class FakeTenantScope : ITenantScope
{
    public Guid? Current { get; private set; }

    public List<Guid> Begun { get; } = [];

    public IDisposable Begin(Guid tenantId)
    {
        var previous = Current;
        Current = tenantId;
        Begun.Add(tenantId);
        return new Restore(() => Current = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

internal sealed class FakeDirectory : IInstitutionDirectory
{
    private readonly Dictionary<string, Guid> _codes = new(StringComparer.Ordinal);

    public FakeDirectory Add(string code, Guid tenantId)
    {
        _codes[code] = tenantId;
        return this;
    }

    public Task<Guid?> FindIdByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(_codes.TryGetValue(code.Trim().ToUpperInvariant(), out var id) ? id : (Guid?)null);
}

internal sealed class FakeAccounts(FakeTenantScope scope) : IStaffAccountStore
{
    public List<StaffAccount> All { get; } = [];

    public int Saves { get; private set; }

    public Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Visible().SingleOrDefault(a => a.Email == normalizedEmail));

    public Task<StaffAccount?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(Visible().SingleOrDefault(a => a.Id == accountId));

    public Task AddAsync(StaffAccount account, CancellationToken cancellationToken)
    {
        All.Add(account);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }

    // No tenant = no rows (fail closed), as the real filter.
    private IEnumerable<StaffAccount> Visible() => All.Where(a => scope.Current is { } t && a.TenantId == t);
}

internal sealed class FakeSessions(FakeTenantScope scope) : ISessionStore
{
    public List<Session> All { get; } = [];

    public Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(All.SingleOrDefault(s => s.Id == sessionId && scope.Current is { } t && s.TenantId == t));

    public Task AddAsync(Session session, CancellationToken cancellationToken)
    {
        if (scope.Current != session.TenantId)
        {
            throw new InvalidOperationException("Session added outside its tenant scope.");
        }

        All.Add(session);
        return Task.CompletedTask;
    }

    public Task CloseOthersAsync(Guid accountId, Guid keepSessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var session in All.Where(s => s.AccountId == accountId && s.Id != keepSessionId))
        {
            session.Close(now);
        }

        return Task.CompletedTask;
    }

    public Task<bool> CloseAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var session = All.SingleOrDefault(s => s.Id == sessionId && scope.Current is { } t && s.TenantId == t && s.ClosedAt is null);
        session?.Close(now);
        return Task.FromResult(session is not null);
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeThrottles : ILoginThrottleStore
{
    public Dictionary<string, LoginThrottle> All { get; } = new(StringComparer.Ordinal);

    public Task<LoginThrottle?> FindAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(All.GetValueOrDefault(key));

    public Task<T> UpdateAsync<T>(string key, Func<LoginThrottle, T> change, CancellationToken cancellationToken)
    {
        if (!All.TryGetValue(key, out var throttle))
        {
            All[key] = throttle = LoginThrottle.Create(key);
        }

        return Task.FromResult(change(throttle));
    }
}

/// <summary>"hash:" + password — and it counts verifications, null hashes included (timing parity, R9).</summary>
internal sealed class FakeHasher : IPasswordHasher
{
    public int Verifications { get; private set; }

    public string Hash(string password) => "hash:" + password;

    public bool Verify(string? passwordHash, string password)
    {
        Verifications++;
        return passwordHash is not null && passwordHash == Hash(password);
    }
}
