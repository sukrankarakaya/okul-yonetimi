using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Domain;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Domain;

/// <summary>Spec 0004 — who must use 2FA (Requirement 1), code reuse (5), who may turn it off (7), the login step (4). Pure.</summary>
public sealed class TwoFactorRulesTests
{
    [Theory]
    [InlineData(new[] { StaffRoles.Administrator }, true)]
    [InlineData(new[] { StaffRoles.Accountant }, true)]
    [InlineData(new[] { StaffRoles.Teacher, StaffRoles.Accountant }, true)]
    [InlineData(new[] { StaffRoles.Teacher }, false)]
    public void Mandatory_WhenAnyRoleRequiresIt(string[] roles, bool required)
    {
        Account(roles).TwoFactorRequired.ShouldBe(required);
    }

    [Fact]
    public void A_Step_IsAcceptedOnce_AndNeverAnOlderOne()
    {
        var account = Account([StaffRoles.Administrator]);
        account.TryAcceptStep(100).ShouldBeFalse(); // 2FA not on yet

        account.EnableTwoFactor("protected", confirmedStep: 100);

        account.TryAcceptStep(100).ShouldBeFalse(); // the confirming code is used
        account.TryAcceptStep(101).ShouldBeTrue();
        account.TryAcceptStep(101).ShouldBeFalse();
        account.TryAcceptStep(99).ShouldBeFalse();
        account.TryAcceptStep(103).ShouldBeTrue();
    }

    [Fact]
    public void OnlyA_TeacherOnlyAccount_CanTurnItOff()
    {
        var teacher = Account([StaffRoles.Teacher]);
        teacher.CanDisableTwoFactor.ShouldBeFalse(); // nothing to turn off
        teacher.EnableTwoFactor("protected", 1);
        teacher.CanDisableTwoFactor.ShouldBeTrue();
        teacher.ClearTwoFactor();
        teacher.TwoFactorEnabled.ShouldBeFalse();

        var both = Account([StaffRoles.Teacher, StaffRoles.Accountant]);
        both.EnableTwoFactor("protected", 1);
        both.CanDisableTwoFactor.ShouldBeFalse();
    }

    [Fact]
    public void LoginStep_LastsFiveMinutes_AndEndsOnce()
    {
        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-20T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var step = LoginChallenge.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            LoginChallengePurpose.Verify, ClientKind.Web, time.GetUtcNow(), "hash");

        time.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        step.IsUsable(time.GetUtcNow()).ShouldBeTrue();
        time.Advance(TimeSpan.FromSeconds(1));
        step.IsUsable(time.GetUtcNow()).ShouldBeFalse();

        var cancelled = LoginChallenge.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            LoginChallengePurpose.Setup, ClientKind.Mobile, time.GetUtcNow(), "hash");
        cancelled.End(time.GetUtcNow());
        cancelled.IsUsable(time.GetUtcNow()).ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => step.SetPendingSecret("x")); // a verify step carries no secret
    }

    private static StaffAccount Account(string[] roles) =>
        StaffAccount.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "a@okul.test", "h", roles);
}
