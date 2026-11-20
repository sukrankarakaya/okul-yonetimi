using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Identity.Domain;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Application;

/// <summary>
/// Spec 0007 — Requirement 7, 10; AC-5, AC-7: Kimlik opens veli sessions for Kayıt (ADR 0002), renews them only while
/// Kayıt knows the veli, with the veli role only; the reset command closes them all.
/// </summary>
public sealed class RefreshSessionTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Guardian = Guid.CreateVersion7();

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeTenantScope _scope = new();
    private readonly FakeSessions _sessions;
    private readonly FakeAccounts _accounts;
    private readonly FakeGuardianDirectory _guardians = new();

    public RefreshSessionTests()
    {
        _sessions = new FakeSessions(_scope);
        _accounts = new FakeAccounts(_scope);
        _guardians.Active.Add((Tenant, Guardian));
    }

    private GuardianSessions Sessions => new(_scope, _sessions, _clock);

    private Task<RefreshResult> Refresh(string token) =>
        new RefreshSessionCommandHandler(_scope, _sessions, _accounts, _guardians, _clock)
            .Handle(new RefreshSessionCommand(token), TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(SessionClient.Web, 12 * 60)]
    [InlineData(SessionClient.Mobile, 30 * 24 * 60)]
    public async Task GuardianSession_OpensInItsKurum_WithTheVeliRoleOnly(SessionClient client, int minutes)
    {
        var started = await Sessions.StartAsync(Tenant, Guardian, client, TestContext.Current.CancellationToken);

        started.TenantId.ShouldBe(Tenant);
        started.GuardianId.ShouldBe(Guardian);
        started.Roles.ShouldBe([StaffRoles.Guardian]);
        started.ExpiresAt.ShouldBe(_clock.GetUtcNow().AddMinutes(minutes));
        var stored = _sessions.All.ShouldHaveSingleItem();
        stored.Kind.ShouldBe(AccountKind.Guardian);
        stored.TenantId.ShouldBe(Tenant);
    }

    [Fact]
    public async Task GuardianSession_RenewsSilently_WithTheVeliRole()
    {
        var started = await Sessions.StartAsync(Tenant, Guardian, SessionClient.Mobile, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(16));

        var renewed = await Refresh(started.RefreshToken);

        renewed.Status.ShouldBe(RefreshStatus.Renewed);
        renewed.Session!.AccountId.ShouldBe(Guardian);
        renewed.Session.Roles.ShouldBe([StaffRoles.Guardian]);
        (await Refresh(renewed.Session.RefreshToken)).Status.ShouldBe(RefreshStatus.Renewed);
    }

    [Fact]
    public async Task GuardianSession_IsClosed_WhenKayitNoLongerKnowsTheVeli()
    {
        var started = await Sessions.StartAsync(Tenant, Guardian, SessionClient.Web, TestContext.Current.CancellationToken);
        _guardians.Active.Clear();

        (await Refresh(started.RefreshToken)).Status.ShouldBe(RefreshStatus.Rejected);
        _sessions.All.Single().ClosedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GuardianSession_EndsAtTheAbsoluteLimit()
    {
        var started = await Sessions.StartAsync(Tenant, Guardian, SessionClient.Web, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromHours(12));

        (await Refresh(started.RefreshToken)).Status.ShouldBe(RefreshStatus.Rejected);
    }

    [Fact]
    public async Task CloseAll_ClosesEverySessionOfTheVeli_AndNoOneElses()
    {
        var ct = TestContext.Current.CancellationToken;
        await Sessions.StartAsync(Tenant, Guardian, SessionClient.Web, ct);
        await Sessions.StartAsync(Tenant, Guardian, SessionClient.Mobile, ct);
        var other = await Sessions.StartAsync(Tenant, Guid.CreateVersion7(), SessionClient.Web, ct);

        (await Sessions.CloseAllAsync(Tenant, Guardian, ct)).ShouldBe(2);

        _sessions.All.Where(s => s.AccountId == Guardian).ShouldAllBe(s => s.ClosedAt != null);
        _sessions.All.Single(s => s.Id == other.SessionId).ClosedAt.ShouldBeNull();
    }
}
