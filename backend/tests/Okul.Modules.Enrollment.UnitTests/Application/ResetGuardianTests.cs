using Okul.Modules.Enrollment.Application;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Application;

/// <summary>Spec 0007 — Requirement 10 (K8), AC-7: the reset command closes sessions and lifts the lock.</summary>
public sealed class ResetGuardianTests
{
    private readonly LoginWorld _w = new();

    [Fact]
    public async Task Reset_ClosesTheVelisSessions_LiftsTheLock_AndEndsThePendingCode()
    {
        await _w.Request();
        await _w.Verify(_w.LastCode);
        await _w.Request();
        var pending = _w.LastCode!;
        for (var i = 0; i < 5; i++)
        {
            await _w.Verify(LoginWorld.Other(pending));
        }

        (await _w.Verify(pending)).Status.ShouldBe(VerifyCodeStatus.Locked);

        var result = await _w.Reset(phone: "+90 532 123 45 67");

        result.Status.ShouldBe(ResetGuardianStatus.Reset);
        result.ClosedSessions.ShouldBe(1);
        _w.Sessions.Closed.ShouldBe([(LoginWorld.TenantA, _w.Ayse.Id)]);
        (await _w.Verify(pending)).Status.ShouldBe(VerifyCodeStatus.Wrong); // unlocked; the old code is gone
        await _w.Request();
        (await _w.Verify(_w.LastCode)).Status.ShouldBe(VerifyCodeStatus.Succeeded);
    }

    [Theory]
    [InlineData("YOK-BOYLE", LoginWorld.AysePhone)]
    [InlineData(LoginWorld.KurumA, "0533 000 00 00")]
    [InlineData(LoginWorld.KurumA, "bozuk")]
    public async Task UnknownKurumOrTelefon_IsAClearError(string kurum, string phone)
    {
        (await _w.Reset(kurum, phone)).Status.ShouldBe(ResetGuardianStatus.NotFound);

        _w.Sessions.Closed.ShouldBeEmpty();
    }
}
