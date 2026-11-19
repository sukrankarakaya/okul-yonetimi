using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Domain;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Domain;

/// <summary>Spec 0003 — Requirement 1 (e-posta), 2 (roles), 7 (password rule). Pure, fast.</summary>
public sealed class EmailAndAccountTests
{
    [Theory]
    [InlineData("ali@okul.test", "ali@okul.test")]
    [InlineData("  Ali@Okul.Test ", "ali@okul.test")]
    [InlineData("ALİ@OKUL.TEST", "ali@okul.test")]
    [InlineData("alı@okul.test", "ali@okul.test")]
    [InlineData("IŞIK@okul.test", "işik@okul.test")]
    public void Email_IsCaseSpaceAndTurkishIInsensitive(string input, string expected)
    {
        EmailAddress.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ali")]
    [InlineData("@okul.test")]
    [InlineData("ali@")]
    [InlineData("ali@@okul.test")]
    [InlineData("ali veli@okul.test")]
    public void Email_RejectsWhatIsNotAnAddress(string? input)
    {
        EmailAddress.Normalize(input).ShouldBeNull();
    }

    [Fact]
    public void Account_NeedsAStaffRole_ToLogInAsStaff_AndKeepsAllItsRoles()
    {
        var both = StaffAccount.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "a@okul.test", "h",
            [StaffRoles.Teacher, StaffRoles.Accountant, StaffRoles.Teacher]);
        var guardian = StaffAccount.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "v@okul.test", "h",
            [StaffRoles.Guardian]);

        both.Roles.ShouldBe([StaffRoles.Teacher, StaffRoles.Accountant]);
        both.CanLogInAsStaff.ShouldBeTrue();
        guardian.CanLogInAsStaff.ShouldBeFalse();
        Should.Throw<ArgumentException>(() =>
            StaffAccount.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "x@okul.test", "h", ["Root"]));
        Should.Throw<ArgumentException>(() =>
            StaffAccount.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "x@okul.test", "h", []));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("123456789", false)]
    [InlineData("1234567890", true)]
    public void PasswordPolicy_NeedsTenCharacters(string? password, bool acceptable)
    {
        PasswordPolicy.IsAcceptable(password).ShouldBe(acceptable);
    }
}

/// <summary>Spec 0003, Requirement 4 / AC-3 — the lockout rules on a fake clock.</summary>
public sealed class LoginThrottleTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void FiveFailures_LockFor15Minutes_ThenUnlock()
    {
        var throttle = LoginThrottle.Create("k");

        for (var i = 1; i <= 4; i++)
        {
            throttle.RegisterFailure(_clock.GetUtcNow()).ShouldBeFalse();
        }

        throttle.RegisterFailure(_clock.GetUtcNow()).ShouldBeTrue();
        throttle.IsLocked(_clock.GetUtcNow()).ShouldBeTrue();

        _clock.Advance(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1));
        throttle.IsLocked(_clock.GetUtcNow()).ShouldBeTrue();
        _clock.Advance(TimeSpan.FromSeconds(1));
        throttle.IsLocked(_clock.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void FailuresWhileLocked_DoNotExtendTheLock()
    {
        var throttle = Locked();

        _clock.Advance(TimeSpan.FromMinutes(14));
        throttle.RegisterFailure(_clock.GetUtcNow()).ShouldBeFalse();
        throttle.RegisterFailure(_clock.GetUtcNow()).ShouldBeFalse();
        _clock.Advance(TimeSpan.FromMinutes(1));

        throttle.IsLocked(_clock.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void FourFailures_Success_FourFailures_DoNotLock()
    {
        var throttle = LoginThrottle.Create("k");
        Fail(throttle, 4);
        throttle.Reset();
        Fail(throttle, 4);

        throttle.IsLocked(_clock.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void FourFailures_QuietFifteenMinutes_FourFailures_DoNotLock()
    {
        var throttle = LoginThrottle.Create("k");
        Fail(throttle, 4);
        _clock.Advance(TimeSpan.FromMinutes(15));
        Fail(throttle, 4);

        throttle.IsLocked(_clock.GetUtcNow()).ShouldBeFalse();
        throttle.RegisterFailure(_clock.GetUtcNow()).ShouldBeTrue();
    }

    [Fact]
    public void FourFailures_ShortPause_OneMore_Locks()
    {
        var throttle = LoginThrottle.Create("k");
        Fail(throttle, 4);
        _clock.Advance(TimeSpan.FromMinutes(14));

        throttle.RegisterFailure(_clock.GetUtcNow()).ShouldBeTrue();
    }

    private LoginThrottle Locked()
    {
        var throttle = LoginThrottle.Create("k");
        Fail(throttle, 5);
        throttle.IsLocked(_clock.GetUtcNow()).ShouldBeTrue();
        return throttle;
    }

    private void Fail(LoginThrottle throttle, int times)
    {
        for (var i = 0; i < times; i++)
        {
            throttle.RegisterFailure(_clock.GetUtcNow());
        }
    }
}

/// <summary>Spec 0003, Requirements 5–6 / AC-5, AC-6 — session lifetime and refresh rotation on a fake clock.</summary>
public sealed class SessionTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(ClientKind.Web, 12 * 60)]
    [InlineData(ClientKind.Mobile, 30 * 24 * 60)]
    public void AbsoluteLimit_IsFixedFromLogin_AndUseNeverExtendsIt(ClientKind client, int minutes)
    {
        var session = Start(client, "h0");
        var hash = "h0";

        for (var i = 1; _clock.GetUtcNow() < session.StartedAt.AddMinutes(minutes - 15); i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(14));
            session.Refresh(hash, hash = $"h{i}", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.Rotated);
        }

        session.ExpiresAt.ShouldBe(session.StartedAt.AddMinutes(minutes));
        _clock.SetUtcNow(session.ExpiresAt);
        session.IsActive(_clock.GetUtcNow()).ShouldBeFalse();
        session.Refresh(hash, "late", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.Ended);
    }

    [Fact]
    public void ParallelRefresh_WithinGrace_ChangesNothing_AfterGrace_IsReuse_AndCloses()
    {
        var session = Start(ClientKind.Web, "h0");
        session.Refresh("h0", "h1", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.Rotated);

        _clock.Advance(TimeSpan.FromSeconds(10));
        session.Refresh("h0", "x", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.ParallelWithinGrace);
        session.CurrentRefreshHash.ShouldBe("h1");
        session.IsActive(_clock.GetUtcNow()).ShouldBeTrue();

        _clock.Advance(TimeSpan.FromSeconds(1));
        session.Refresh("h0", "x", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.Reused);
        session.IsActive(_clock.GetUtcNow()).ShouldBeFalse();
        session.Refresh("h1", "y", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.Ended);
    }

    [Fact]
    public void AnOlderCredential_IsReuse_EvenWithinGrace()
    {
        var session = Start(ClientKind.Mobile, "h0");
        session.Refresh("h0", "h1", _clock.GetUtcNow());
        session.Refresh("h1", "h2", _clock.GetUtcNow());

        session.Refresh("h0", "x", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.Reused);
        session.IsActive(_clock.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void ClosedSession_IsInactiveAtOnce_AndCannotRefresh()
    {
        var session = Start(ClientKind.Web, "h0");

        session.Close(_clock.GetUtcNow());

        session.IsActive(_clock.GetUtcNow()).ShouldBeFalse();
        session.Refresh("h0", "h1", _clock.GetUtcNow()).ShouldBe(RefreshOutcome.Ended);
    }

    private Session Start(ClientKind client, string hash) =>
        Session.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), client, _clock.GetUtcNow(), hash);
}
