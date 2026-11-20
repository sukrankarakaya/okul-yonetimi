using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Contracts;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Identity.Infrastructure;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>
/// Spec 0003, plan 0003 step 3 — Kimlik storage on the real PostgreSQL 18: both migrations apply, roles round-trip,
/// the tenant filter hides another kurum's accounts and sessions (BR-1), closing other sessions is one filtered
/// update, and the tenant-free login throttle stores hashed keys only.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IdentityPersistenceTests
{
    private static readonly Guid TenantA = Guid.CreateVersion7();
    private static readonly Guid TenantB = Guid.CreateVersion7();
    private readonly ApiFixture _api;

    public IdentityPersistenceTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task Accounts_AndSessions_AreTenantFiltered_AndRolesRoundTrip()
    {
        var ct = TestContext.Current.CancellationToken;
        await MigrateAsync(ct);
        var accountA = StaffAccount.Create(Guid.CreateVersion7(), TenantA, $"a-{Guid.NewGuid():N}@okul.test", "hash",
            [StaffRoles.Teacher, StaffRoles.Accountant]);
        var accountB = StaffAccount.Create(Guid.CreateVersion7(), TenantB, accountA.Email, "hash", [StaffRoles.Administrator]);
        var now = DateTimeOffset.UtcNow;
        var here = Session.Start(Guid.CreateVersion7(), TenantA, accountA.Id, ClientKind.Web, now, "h1");
        var other = Session.Start(Guid.CreateVersion7(), TenantA, accountA.Id, ClientKind.Mobile, now, "h2");
        var foreign = Session.Start(Guid.CreateVersion7(), TenantB, accountB.Id, ClientKind.Web, now, "h3");

        await using (var db = Identity(null))
        {
            db.StaffAccounts.AddRange(accountA, accountB);
            db.Sessions.AddRange(here, other, foreign);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = Identity(TenantA))
        {
            var visible = await db.StaffAccounts.Where(a => a.Email == accountA.Email).ToListAsync(ct);
            visible.ShouldHaveSingleItem().Id.ShouldBe(accountA.Id);
            visible[0].Roles.ShouldBe([StaffRoles.Teacher, StaffRoles.Accountant]);
            (await db.Sessions.AnyAsync(s => s.Id == foreign.Id, ct)).ShouldBeFalse();

        }

        // The real store, resolved the way the API will: module registration + the current tenant.
        await using (var provider = Services(TenantA))
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISessionStore>().CloseOthersAsync(accountA.Id, here.Id, now, ct);
        }

        await using (var db = Identity(TenantA))
        {
            (await db.Sessions.SingleAsync(s => s.Id == here.Id, ct)).ClosedAt.ShouldBeNull();
            (await db.Sessions.SingleAsync(s => s.Id == other.Id, ct)).ClosedAt.ShouldNotBeNull();
        }

        await using (var db = Identity(TenantB))
        {
            (await db.Sessions.SingleAsync(s => s.Id == foreign.Id, ct)).ClosedAt.ShouldBeNull();
        }

        await using (var db = Identity(null))
        {
            (await db.StaffAccounts.AnyAsync(ct)).ShouldBeFalse(); // no tenant = nothing (fail closed)
        }
    }

    /// <summary>
    /// Review round 1 #5 — two requests that read the same session and both rotate it: only the first write lands, the
    /// second is refused (optimistic concurrency on the current refresh hash) instead of silently overwriting it.
    /// </summary>
    [Fact]
    public async Task TwoRotationsOfOneReadSession_OnlyTheFirstIsWritten()
    {
        var ct = TestContext.Current.CancellationToken;
        await MigrateAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var session = Session.Start(Guid.CreateVersion7(), TenantA, Guid.CreateVersion7(), ClientKind.Web, now, "h0");
        await using (var db = Identity(null))
        {
            db.Sessions.Add(session);
            await db.SaveChangesAsync(ct);
        }

        await using var provider = Services(TenantA);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var storeA = first.ServiceProvider.GetRequiredService<ISessionStore>();
        var storeB = second.ServiceProvider.GetRequiredService<ISessionStore>();
        var readA = (await storeA.FindAsync(session.Id, ct))!;
        var readB = (await storeB.FindAsync(session.Id, ct))!;

        readA.Refresh("h0", "hA", now).ShouldBe(RefreshOutcome.Rotated);
        readB.Refresh("h0", "hB", now).ShouldBe(RefreshOutcome.Rotated);
        (await storeA.TrySaveChangesAsync(ct)).ShouldBeTrue();
        (await storeB.TrySaveChangesAsync(ct)).ShouldBeFalse();

        await using var db2 = Identity(TenantA);
        (await db2.Sessions.SingleAsync(s => s.Id == session.Id, ct)).CurrentRefreshHash.ShouldBe("hA");
    }

    /// <summary>
    /// Review round 2 L-1 — the theft signal still closes the session when a rotation lands between the handler's read
    /// and its write: the conditional save fails, the handler falls back to the bulk close. The race is staged
    /// deterministically: right after the handler reads the session, another scope rotates it for real.
    /// </summary>
    [Fact]
    public async Task ReusedCredential_DuringAConcurrentRotation_StillClosesTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        await MigrateAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var session = Session.Start(Guid.CreateVersion7(), TenantA, Guid.CreateVersion7(), ClientKind.Web, now, "h0");
        await using (var db = Identity(null))
        {
            db.Sessions.Add(session);
            await db.SaveChangesAsync(ct);
        }

        await using var provider = Services(TenantA);
        await using var handlerScope = provider.CreateAsyncScope();
        var racing = new RotateAfterFind(handlerScope.ServiceProvider.GetRequiredService<ISessionStore>(), async () =>
        {
            await using var other = provider.CreateAsyncScope();
            var store = other.ServiceProvider.GetRequiredService<ISessionStore>();
            (await store.FindAsync(session.Id, ct))!.Refresh("h0", "hA", now).ShouldBe(RefreshOutcome.Rotated);
            (await store.TrySaveChangesAsync(ct)).ShouldBeTrue();
        });
        var handler = new RefreshSessionCommandHandler(
            new PassThroughScope(), racing, handlerScope.ServiceProvider.GetRequiredService<IStaffAccountStore>(), new NoGuardians(),
            TimeProvider.System);
        var stolen = new RefreshCredential(TenantA, session.Id, new string('x', 43)); // neither current nor previous

        var result = await handler.Handle(new RefreshSessionCommand(stolen.ToString()), ct);

        result.Status.ShouldBe(RefreshStatus.Rejected);
        racing.Rotated.ShouldBeTrue();
        await using var check = Identity(TenantA);
        var stored = await check.Sessions.SingleAsync(s => s.Id == session.Id, ct);
        stored.CurrentRefreshHash.ShouldBe("hA");     // the concurrent rotation landed …
        stored.ClosedAt.ShouldNotBeNull();            // … and the theft signal closed the session anyway
    }

    [Fact]
    public async Task LoginThrottle_IsStoredByHashedKey_WithoutATenant()
    {
        var ct = TestContext.Current.CancellationToken;
        await MigrateAsync(ct);
        var key = Convert.ToHexString(Guid.NewGuid().ToByteArray()).PadRight(64, '0');
        var now = DateTimeOffset.UtcNow;

        await using (var db = Throttles())
        {
            var throttle = LoginThrottle.Create(key);
            for (var i = 0; i < LoginThrottle.MaxFailures; i++)
            {
                throttle.RegisterFailure(now);
            }

            db.LoginThrottles.Add(throttle);
            await db.SaveChangesAsync(ct);
        }

        await using (var db = Throttles())
        {
            var stored = await db.LoginThrottles.SingleAsync(t => t.Key == key, ct);
            stored.IsLocked(now).ShouldBeTrue();
            stored.LockedUntil!.Value.ShouldBe(now + LoginThrottle.LockDuration, TimeSpan.FromMilliseconds(1));
        }
    }

    private async Task MigrateAsync(CancellationToken ct)
    {
        await using (var db = Identity(null))
        {
            await db.Database.MigrateAsync(ct);
        }

        await using (var db = Throttles())
        {
            await db.Database.MigrateAsync(ct);
        }
    }

    private IdentityDbContext Identity(Guid? tenant) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_api.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options, new FixedTenant(tenant));

    private LoginThrottleDbContext Throttles() =>
        new(new DbContextOptionsBuilder<LoginThrottleDbContext>()
            .UseNpgsql(_api.ConnectionString, npgsql => npgsql.MigrationsHistoryTable(LoginThrottleDbContext.HistoryTable, IdentityDbContext.Schema))
            .Options);

    private sealed class FixedTenant(Guid? tenant) : ITenantContext
    {
        public Guid? TenantId => tenant;
    }

    private ServiceProvider Services(Guid tenant)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new FixedTenant(tenant));
        services.AddSingleton<ITenantScope, NoScope>();
        services.AddSingleton(TimeProvider.System);
        services.AddIdentityInfrastructure(_api.ConnectionString, new TwoFactorKey(_api.TwoFactorKey));
        return services.BuildServiceProvider();
    }

    /// <summary>The filter's tenant comes from <see cref="FixedTenant"/>; the handler's scope is a no-op here.</summary>
    private sealed class PassThroughScope : ITenantScope
    {
        public IDisposable Begin(Guid tenantId) => new Nothing();

        private sealed class Nothing : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    /// <summary>Runs <c>afterFind</c> once, right after the first read — the staged concurrent rotation.</summary>
    private sealed class RotateAfterFind(ISessionStore inner, Func<Task> afterFind) : ISessionStore
    {
        public bool Rotated { get; private set; }

        public async Task<Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            var session = await inner.FindAsync(sessionId, cancellationToken);
            if (!Rotated)
            {
                Rotated = true;
                await afterFind();
            }

            return session;
        }

        public Task AddAsync(Session session, CancellationToken cancellationToken) => inner.AddAsync(session, cancellationToken);

        public Task CloseOthersAsync(Guid accountId, Guid keepSessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
            inner.CloseOthersAsync(accountId, keepSessionId, now, cancellationToken);

        public Task<int> CloseAllAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken) =>
            inner.CloseAllAsync(accountId, now, cancellationToken);

        public Task<bool> CloseAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
            inner.CloseAsync(sessionId, now, cancellationToken);

        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken) => inner.TrySaveChangesAsync(cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class NoScope : ITenantScope
    {
        public IDisposable Begin(Guid tenantId) => throw new NotSupportedException();
    }

    /// <summary>A staff session is renewed here; Kayıt is never asked (spec 0007 changes nothing for staff).</summary>
    private sealed class NoGuardians : IGuardianDirectory
    {
        public Task<bool> IsActiveAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasCurrentConsentAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
