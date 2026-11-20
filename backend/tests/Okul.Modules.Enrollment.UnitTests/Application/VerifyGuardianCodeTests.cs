using Okul.Modules.Enrollment.Application;
using Okul.Modules.Identity.Contracts;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Application;

/// <summary>Spec 0007 — Requirements 3, 4, 7, 9; AC-2, AC-3, AC-6, AC-7 (handler level).</summary>
public sealed class VerifyGuardianCodeTests
{
    private readonly LoginWorld _w = new();

    [Fact]
    public async Task RightCode_OpensAVeliSession_InTheKurumOfTheCode_AndCreatesTheAccount()
    {
        await _w.Request();

        var result = await _w.Verify(_w.LastCode);

        result.Status.ShouldBe(VerifyCodeStatus.Succeeded);
        var session = _w.Sessions.Started.ShouldHaveSingleItem();
        session.TenantId.ShouldBe(LoginWorld.TenantA);
        session.GuardianId.ShouldBe(_w.Ayse.Id);
        session.Client.ShouldBe(SessionClient.Mobile);
        result.ConsentRequired.ShouldBeTrue();
        _w.Ayse.HasAccount.ShouldBeTrue();
        _w.AyseInB.HasAccount.ShouldBeFalse();
    }

    [Fact]
    public async Task SecondLogin_UsesTheSameAccount()
    {
        await _w.Request();
        await _w.Verify(_w.LastCode);
        var created = _w.Ayse.AccountCreatedAt;
        _w.Clock.Advance(TimeSpan.FromDays(1));

        await _w.Request();
        (await _w.Verify(_w.LastCode)).Status.ShouldBe(VerifyCodeStatus.Succeeded);

        _w.Ayse.AccountCreatedAt.ShouldBe(created);
        _w.Sessions.Started.Select(s => s.GuardianId).Distinct().ShouldBe([_w.Ayse.Id]);
    }

    [Fact]
    public async Task TheSameTelefonInAnotherKurum_IsAnotherVeli()
    {
        await _w.Request(kurum: LoginWorld.KurumB);

        (await _w.Verify(_w.LastCode, kurum: LoginWorld.KurumB)).Status.ShouldBe(VerifyCodeStatus.Succeeded);
        (await _w.Verify(_w.LastCode, kurum: LoginWorld.KurumA)).Status.ShouldBe(VerifyCodeStatus.Wrong);

        _w.Sessions.Started.ShouldHaveSingleItem().GuardianId.ShouldBe(_w.AyseInB.Id);
    }

    [Fact]
    public async Task UsedCode_IsWrong()
    {
        await _w.Request();
        var code = _w.LastCode;
        await _w.Verify(code);

        (await _w.Verify(code)).Status.ShouldBe(VerifyCodeStatus.Wrong);
    }

    [Fact]
    public async Task WrongCode_IsWrong_AndTheRightOneStillWorks()
    {
        await _w.Request();

        (await _w.Verify(LoginWorld.Other(_w.LastCode!))).Status.ShouldBe(VerifyCodeStatus.Wrong);
        (await _w.Verify(_w.LastCode)).Status.ShouldBe(VerifyCodeStatus.Succeeded);
    }

    [Fact]
    public async Task CodeWithSpaces_IsAccepted()
    {
        await _w.Request();
        var code = _w.LastCode!;

        (await _w.Verify($" {code[..3]} {code[3..]} ")).Status.ShouldBe(VerifyCodeStatus.Succeeded);
    }

    [Fact]
    public async Task AfterFiveMinutes_TheCodeHasExpired()
    {
        await _w.Request();
        _w.Clock.Advance(TimeSpan.FromMinutes(5));

        (await _w.Verify(_w.LastCode)).Status.ShouldBe(VerifyCodeStatus.Expired);
        _w.Sessions.Started.ShouldBeEmpty();
    }

    [Fact]
    public async Task Vazgec_CancelsThePendingCode()
    {
        await _w.Request();

        (await _w.Cancel()).ShouldBeTrue();

        (await _w.Verify(_w.LastCode)).Status.ShouldBe(VerifyCodeStatus.Wrong);
    }

    [Fact]
    public async Task FiveWrongCodes_Lock_AndTheRightCodeIsRefusedWhileLocked_ThenWorksAfterFifteenMinutes()
    {
        await _w.Request();
        var right = _w.LastCode!;
        for (var i = 0; i < 4; i++)
        {
            (await _w.Verify(LoginWorld.Other(right))).Status.ShouldBe(VerifyCodeStatus.Wrong);
        }

        (await _w.Verify(LoginWorld.Other(right))).Status.ShouldBe(VerifyCodeStatus.Locked);
        (await _w.Verify(right)).Status.ShouldBe(VerifyCodeStatus.Locked);

        _w.Clock.Advance(TimeSpan.FromMinutes(15));
        await _w.Request();
        (await _w.Verify(_w.LastCode)).Status.ShouldBe(VerifyCodeStatus.Succeeded);
    }

    [Fact]
    public async Task UnregisteredTelefon_LocksExactlyTheSameWay()
    {
        await _w.Request(phone: "0533 000 00 00");
        var answers = new List<VerifyCodeStatus>();
        for (var i = 0; i < 6; i++)
        {
            answers.Add((await _w.Verify("123456", phone: "0533 000 00 00")).Status);
        }

        answers.ShouldBe([
            VerifyCodeStatus.Wrong, VerifyCodeStatus.Wrong, VerifyCodeStatus.Wrong, VerifyCodeStatus.Wrong,
            VerifyCodeStatus.Locked, VerifyCodeStatus.Locked]);
    }

    [Fact]
    public async Task UnregisteredTelefon_ExpiresTheSameWay()
    {
        await _w.Request(phone: "0533 000 00 00");
        _w.Clock.Advance(TimeSpan.FromMinutes(5));

        (await _w.Verify("123456", phone: "0533 000 00 00")).Status.ShouldBe(VerifyCodeStatus.Expired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public async Task WhatIsNotACode_IsWrong(string? code)
    {
        await _w.Request();

        (await _w.Verify(code)).Status.ShouldBe(VerifyCodeStatus.Wrong);
    }

    [Fact]
    public async Task VeliWithCurrentConsent_IsNotAskedAgain()
    {
        await _w.Accept(_w.Ayse);
        await _w.Request();

        (await _w.Verify(_w.LastCode)).ConsentRequired.ShouldBeFalse();
    }
}
