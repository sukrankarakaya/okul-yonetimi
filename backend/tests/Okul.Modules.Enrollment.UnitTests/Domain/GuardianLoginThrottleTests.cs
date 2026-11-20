using Okul.Modules.Enrollment.Domain;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Domain;

/// <summary>Spec 0007 — Requirement 4, AC-3 (5 wrong codes / 15 minutes; 3 code requests a minute, G-2).</summary>
public sealed class GuardianLoginThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FiveWrongCodes_LockForFifteenMinutes_AndAttemptsDoNotExtendIt()
    {
        var throttle = GuardianLoginThrottle.Create("pair");

        for (var i = 1; i <= 4; i++)
        {
            throttle.RegisterFailure(Start.AddSeconds(i)).ShouldBeFalse();
        }

        throttle.RegisterFailure(Start.AddSeconds(5)).ShouldBeTrue();
        throttle.IsLocked(Start.AddSeconds(5)).ShouldBeTrue();

        throttle.RegisterFailure(Start.AddMinutes(10)).ShouldBeFalse();
        throttle.IsLocked(Start.AddSeconds(5) + GuardianLoginThrottle.LockDuration - TimeSpan.FromSeconds(1)).ShouldBeTrue();
        throttle.IsLocked(Start.AddSeconds(5) + GuardianLoginThrottle.LockDuration).ShouldBeFalse();
    }

    [Fact]
    public void FourFailures_ThenSuccess_ThenFour_DoNotLock()
    {
        var throttle = GuardianLoginThrottle.Create("pair");
        for (var i = 0; i < 4; i++)
        {
            throttle.RegisterFailure(Start.AddSeconds(i));
        }

        throttle.ResetFailures();
        for (var i = 0; i < 4; i++)
        {
            throttle.RegisterFailure(Start.AddSeconds(10 + i)).ShouldBeFalse();
        }

        throttle.IsLocked(Start.AddSeconds(20)).ShouldBeFalse();
    }

    [Fact]
    public void FourFailures_FifteenMinutesApart_DoNotLock()
    {
        var throttle = GuardianLoginThrottle.Create("pair");
        for (var i = 0; i < 4; i++)
        {
            throttle.RegisterFailure(Start.AddSeconds(i));
        }

        var later = Start.AddSeconds(3) + GuardianLoginThrottle.FailureWindow;
        for (var i = 0; i < 4; i++)
        {
            throttle.RegisterFailure(later.AddSeconds(i)).ShouldBeFalse();
        }

        throttle.IsLocked(later.AddSeconds(5)).ShouldBeFalse();
    }

    [Fact]
    public void FourthCodeRequestInAMinute_IsRefused_ThenAllowedAgain()
    {
        var throttle = GuardianLoginThrottle.Create("pair");

        throttle.TryRegisterCodeRequest(Start).ShouldBeTrue();
        throttle.TryRegisterCodeRequest(Start.AddSeconds(20)).ShouldBeTrue();
        throttle.TryRegisterCodeRequest(Start.AddSeconds(40)).ShouldBeTrue();
        throttle.TryRegisterCodeRequest(Start.AddSeconds(59)).ShouldBeFalse();

        throttle.TryRegisterCodeRequest(Start + GuardianLoginThrottle.RequestWindow).ShouldBeTrue();
    }

    [Fact]
    public void ResetFailures_DoesNotResetTheRequestWindow()
    {
        var throttle = GuardianLoginThrottle.Create("pair");
        for (var i = 0; i < 3; i++)
        {
            throttle.TryRegisterCodeRequest(Start.AddSeconds(i));
        }

        throttle.ResetFailures();

        throttle.TryRegisterCodeRequest(Start.AddSeconds(5)).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => GuardianLoginThrottle.Create(" "));
    }
}
