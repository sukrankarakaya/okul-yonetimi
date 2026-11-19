using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Okul.Core.Tenancy;
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
        services.AddIdentityInfrastructure(_api.ConnectionString);
        return services.BuildServiceProvider();
    }

    private sealed class NoScope : ITenantScope
    {
        public IDisposable Begin(Guid tenantId) => throw new NotSupportedException();
    }
}
