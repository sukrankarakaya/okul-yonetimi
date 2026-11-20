using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Application;

/// <summary>Spec 0003 — login, refresh, logout and password change, on in-memory ports and a fake clock.</summary>
public sealed class HandlerTests
{
    private const string Password = "dogru-sifre-123";
    private static readonly Guid TenantA = Guid.CreateVersion7();
    private static readonly Guid TenantB = Guid.CreateVersion7();

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeTenantScope _scope = new();
    private readonly FakeDirectory _directory = new FakeDirectory().Add("OKUL-A", TenantA).Add("OKUL-B", TenantB);
    private readonly FakeThrottles _throttles = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeAccounts _accounts;
    private readonly FakeSessions _sessions;
    private readonly StaffAccount _teacherA;

    public HandlerTests()
    {
        _accounts = new FakeAccounts(_scope);
        _sessions = new FakeSessions(_scope);
        // Two roles, neither making 2FA mandatory (spec 0004): these tests are about the password and the session (A-1).
        _teacherA = Account(TenantA, "ogretmen@okul.test", StaffRoles.Teacher, StaffRoles.Guardian);
        Account(TenantB, "baska@okul.test", StaffRoles.Administrator);
        Account(TenantA, "veli@okul.test", StaffRoles.Guardian);
    }

    [Fact]
    public async Task Login_Succeeds_InTheKurumOfTheCode_WithAllRoles_AndOpensASession()
    {
        var result = await Login("okul-a", " Ogretmen@Okul.Test ", Password, ClientKind.Mobile);

        result.Status.ShouldBe(LoginStatus.Succeeded);
        result.Session!.TenantId.ShouldBe(TenantA);
        result.Session.AccountId.ShouldBe(_teacherA.Id);
        result.Session.Roles.ShouldBe([StaffRoles.Teacher, StaffRoles.Guardian]);
        result.Session.ExpiresAt.ShouldBe(_clock.GetUtcNow() + Session.MobileLifetime);
        _sessions.All.Single().TenantId.ShouldBe(TenantA);
        _scope.Current.ShouldBeNull();
    }

    [Theory]
    [InlineData("OKUL-A", "ogretmen@okul.test", "yanlis-sifre-1")]
    [InlineData("OKUL-A", "yok@okul.test", Password)]
    [InlineData("YOK-KOD", "ogretmen@okul.test", Password)]
    [InlineData("OKUL-A", "baska@okul.test", Password)]
    [InlineData("OKUL-A", "veli@okul.test", Password)]
    [InlineData("", "", "")]
    public async Task Login_WrongInputs_AllGetTheSameAnswer_AndAlwaysVerifyAHash(string code, string email, string password)
    {
        var result = await Login(code, email, password);

        result.ShouldBe(new LoginResult(LoginStatus.InvalidCredentials));
        _hasher.Verifications.ShouldBe(1);
        _sessions.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Login_FiveFailures_Lock_EvenTheRightPassword_AndUnknownAccountsAlike()
    {
        for (var i = 0; i < 5; i++)
        {
            (await Login("OKUL-A", "ogretmen@okul.test", "yanlis-sifre-1")).Status.ShouldBe(LoginStatus.InvalidCredentials);
            (await Login("OKUL-A", "yok@okul.test", "yanlis-sifre-1")).Status.ShouldBe(LoginStatus.InvalidCredentials);
        }

        var verificationsBefore = _hasher.Verifications;
        (await Login("OKUL-A", "ogretmen@okul.test", Password)).Status.ShouldBe(LoginStatus.Locked);
        (await Login("okul-a", "OGRETMEN@okul.test", Password)).Status.ShouldBe(LoginStatus.Locked);
        (await Login("OKUL-A", "yok@okul.test", Password)).Status.ShouldBe(LoginStatus.Locked);
        _hasher.Verifications.ShouldBe(verificationsBefore);

        _clock.Advance(LoginThrottle.LockDuration);
        (await Login("OKUL-A", "ogretmen@okul.test", Password)).Status.ShouldBe(LoginStatus.Succeeded);
    }

    /// <summary>
    /// Review round 1 #3 — an UNKNOWN kurum kodu locks under every spelling (Turkish İ included), exactly like a known
    /// one; otherwise the lock would tell whether a code exists.
    /// </summary>
    [Fact]
    public async Task Login_UnknownCode_LocksUnderEverySpelling()
    {
        for (var i = 0; i < 5; i++)
        {
            (await Login("XYZI", "biri@okul.test", "yanlis-sifre-1")).Status.ShouldBe(LoginStatus.InvalidCredentials);
        }

        (await Login("xyzİ", "biri@okul.test", "yanlis-sifre-1")).Status.ShouldBe(LoginStatus.Locked);
        (await Login(" xyzı ", "BIRI@okul.test", "yanlis-sifre-1")).Status.ShouldBe(LoginStatus.Locked);
    }

    [Fact]
    public async Task Refresh_Rotates_ThenTheOldOneOutsideGrace_ClosesTheSession()
    {
        var first = (await Login("OKUL-A", "ogretmen@okul.test", Password)).Session!;

        var renewed = await Refresh(first.RefreshToken);
        renewed.Status.ShouldBe(RefreshStatus.Renewed);
        renewed.Session!.RefreshToken.ShouldNotBe(first.RefreshToken);
        renewed.Session.Roles.ShouldBe(first.Roles);

        (await Refresh(first.RefreshToken)).Status.ShouldBe(RefreshStatus.Conflict);
        _clock.Advance(Session.ReuseGrace + TimeSpan.FromSeconds(1));
        (await Refresh(first.RefreshToken)).Status.ShouldBe(RefreshStatus.Rejected);
        (await Refresh(renewed.Session.RefreshToken)).Status.ShouldBe(RefreshStatus.Rejected);
    }

    [Fact]
    public async Task Refresh_WithAForgedTenant_OrGarbage_IsRejected()
    {
        var issued = (await Login("OKUL-A", "ogretmen@okul.test", Password)).Session!;
        RefreshCredential.TryParse(issued.RefreshToken, out var credential).ShouldBeTrue();
        var forged = credential!.Value with { TenantId = TenantB };

        (await Refresh(forged.ToString())).Status.ShouldBe(RefreshStatus.Rejected);
        (await Refresh("bozuk")).Status.ShouldBe(RefreshStatus.Rejected);
        (await Refresh(null)).Status.ShouldBe(RefreshStatus.Rejected);
        (await Refresh(issued.RefreshToken)).Status.ShouldBe(RefreshStatus.Renewed);
    }

    [Fact]
    public async Task Logout_ClosesOnlyThisSession_AtOnce()
    {
        var web = (await Login("OKUL-A", "ogretmen@okul.test", Password, ClientKind.Web)).Session!;
        var phone = (await Login("OKUL-A", "ogretmen@okul.test", Password, ClientKind.Mobile)).Session!;
        var validator = new SessionValidator(_scope, _sessions, _clock);

        using (_scope.Begin(TenantA))
        {
            (await new LogoutCommandHandler(_sessions, _clock).Handle(new LogoutCommand(web.SessionId), TestContext.Current.CancellationToken)).ShouldBeTrue();
        }

        (await validator.IsActiveAsync(TenantA, web.SessionId, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await validator.IsActiveAsync(TenantA, phone.SessionId, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await validator.IsActiveAsync(TenantB, phone.SessionId, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await Refresh(web.RefreshToken)).Status.ShouldBe(RefreshStatus.Rejected);
    }

    [Theory]
    [InlineData("yanlis-sifre-1", "yeni-sifre-1234", "yeni-sifre-1234", ChangePasswordStatus.CurrentPasswordWrong)]
    [InlineData(Password, "kisa-12", "kisa-12", ChangePasswordStatus.TooShort)]
    [InlineData(Password, "yeni-sifre-1234", "yeni-sifre-9999", ChangePasswordStatus.Mismatch)]
    public async Task ChangePassword_Rejects_AndChangesNothing(string current, string next, string again, ChangePasswordStatus expected)
    {
        var session = (await Login("OKUL-A", "ogretmen@okul.test", Password)).Session!;

        (await ChangePassword(session, current, next, again)).ShouldBe(expected);

        _teacherA.PasswordHash.ShouldBe(_hasher.Hash(Password));
    }

    [Fact]
    public async Task ChangePassword_Succeeds_OldPasswordFails_OtherSessionsCloseAtOnce_ThisOneStays()
    {
        var here = (await Login("OKUL-A", "ogretmen@okul.test", Password, ClientKind.Web)).Session!;
        var other = (await Login("OKUL-A", "ogretmen@okul.test", Password, ClientKind.Mobile)).Session!;
        var validator = new SessionValidator(_scope, _sessions, _clock);

        (await ChangePassword(here, Password, "yeni-sifre-1234", "yeni-sifre-1234")).ShouldBe(ChangePasswordStatus.Changed);

        (await validator.IsActiveAsync(TenantA, here.SessionId, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await validator.IsActiveAsync(TenantA, other.SessionId, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await Login("OKUL-A", "ogretmen@okul.test", Password)).Status.ShouldBe(LoginStatus.InvalidCredentials);
        (await Login("OKUL-A", "ogretmen@okul.test", "yeni-sifre-1234")).Status.ShouldBe(LoginStatus.Succeeded);
    }

    private StaffAccount Account(Guid tenant, string email, params string[] roles)
    {
        var account = StaffAccount.Create(Guid.CreateVersion7(), tenant, email, _hasher.Hash(Password), roles);
        _accounts.All.Add(account);
        return account;
    }

    private Task<LoginResult> Login(string code, string email, string password, ClientKind client = ClientKind.Web) =>
        new LoginCommandHandler(_directory, _scope, _accounts, _sessions, _throttles, _hasher, new FakeChallenges(_scope), _clock)
            .Handle(new LoginCommand(code, email, password, client), TestContext.Current.CancellationToken);

    private Task<RefreshResult> Refresh(string? token) =>
        new RefreshSessionCommandHandler(_scope, _sessions, _accounts, new FakeGuardianDirectory(), _clock).Handle(new RefreshSessionCommand(token), TestContext.Current.CancellationToken);

    private async Task<ChangePasswordStatus> ChangePassword(IssuedSession session, string current, string next, string again)
    {
        using (_scope.Begin(session.TenantId))
        {
            return await new ChangePasswordCommandHandler(_accounts, _sessions, _hasher, _throttles, _clock)
                .Handle(new ChangePasswordCommand(session.AccountId, session.SessionId, current, next, again), TestContext.Current.CancellationToken);
        }
    }
}
