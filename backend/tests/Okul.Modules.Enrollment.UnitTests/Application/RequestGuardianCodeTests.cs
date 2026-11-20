using Okul.Modules.Enrollment.Application;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Application;

/// <summary>Spec 0007 — Requirements 1–5; AC-1, AC-3, AC-8 (handler level; the API level is in GuardianLoginTests).</summary>
public sealed class RequestGuardianCodeTests
{
    private readonly LoginWorld _w = new();

    [Fact]
    public async Task RegisteredVeli_GetsASixDigitCode_BySms()
    {
        (await _w.Request()).ShouldBe(RequestCodeStatus.Accepted);

        var sent = _w.Sms.Sent.ShouldHaveSingleItem();
        sent.Phone.ShouldBe("+905321234567");
        sent.Code.Length.ShouldBe(6);
        _w.Login.Codes.ShouldHaveSingleItem().GuardianId.ShouldBe(_w.Ayse.Id);
    }

    [Theory]
    [InlineData("05321234567")]
    [InlineData("+90 532 123 45 67")]
    [InlineData("532-123-45-67")]
    public async Task TheSameNumberWrittenDifferently_FindsTheSameVeli(string phone)
    {
        (await _w.Request(phone: phone)).ShouldBe(RequestCodeStatus.Accepted);

        _w.Sms.Sent.ShouldHaveSingleItem().Phone.ShouldBe("+905321234567");
    }

    [Fact]
    public async Task UnregisteredTelefon_AndUnknownKurum_GetTheSameAnswer_AndNoSms()
    {
        (await _w.Request(phone: "0533 000 00 00")).ShouldBe(RequestCodeStatus.Accepted);
        (await _w.Request(kurum: "YOK-BOYLE")).ShouldBe(RequestCodeStatus.Accepted);
        (await _w.Request(kurum: null)).ShouldBe(RequestCodeStatus.Accepted);

        _w.Sms.Sent.ShouldBeEmpty();
        _w.Login.Codes.Count.ShouldBe(3);
        _w.Login.Codes.ShouldAllBe(c => c.GuardianId == null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0212 123 45 67")]
    [InlineData("532 12")]
    public async Task InvalidTelefon_IsAFormatError(string? phone)
    {
        (await _w.Request(phone: phone)).ShouldBe(RequestCodeStatus.InvalidPhone);

        _w.Login.Codes.ShouldBeEmpty();
    }

    [Fact]
    public async Task FourthRequestInAMinute_IsRateLimited_ThenAllowedAgain()
    {
        for (var i = 0; i < 3; i++)
        {
            (await _w.Request()).ShouldBe(RequestCodeStatus.Accepted);
        }

        (await _w.Request()).ShouldBe(RequestCodeStatus.RateLimited);
        _w.Sms.Sent.Count.ShouldBe(3);

        _w.Clock.Advance(TimeSpan.FromMinutes(1));
        (await _w.Request()).ShouldBe(RequestCodeStatus.Accepted);
    }

    [Fact]
    public async Task UnregisteredTelefon_IsRateLimitedTheSameWay()
    {
        for (var i = 0; i < 3; i++)
        {
            await _w.Request(phone: "0533 000 00 00");
        }

        (await _w.Request(phone: "0533 000 00 00")).ShouldBe(RequestCodeStatus.RateLimited);
    }

    [Fact]
    public async Task ANewCode_EndsThePreviousOne()
    {
        await _w.Request();
        var first = _w.LastCode!;
        await _w.Request();

        (await _w.Verify(first)).Status.ShouldBe(VerifyCodeStatus.Wrong);
        (await _w.Verify(_w.LastCode)).Status.ShouldBe(VerifyCodeStatus.Succeeded);
    }

    [Fact]
    public async Task ProviderDown_IsReported()
    {
        _w.Sms.Fail = true;

        (await _w.Request()).ShouldBe(RequestCodeStatus.SmsFailed);
    }

    [Fact]
    public async Task CodesAreStoredOnlyAsKeyedHashes()
    {
        await _w.Request();

        var stored = _w.Login.Codes.Single();
        stored.CodeHash.ShouldNotContain(_w.LastCode!);
        stored.PairKey.ShouldNotContain("5321234567");
        stored.CodeHash.Length.ShouldBe(64);
    }

    [Fact]
    public async Task EveryAnswer_TakesAtLeastTheMinimum_RegisteredOrNot()
    {
        var world = new LoginWorld { Timing = new GuardianLoginTiming(TimeSpan.FromMilliseconds(200)) };
        var handler = (string phone) => new RequestGuardianCodeCommandHandler(
            world.Institutions, world.Scope, world.Guardians, world.Login, world.Hasher, world.Sms, world.Timing, TimeProvider.System)
            .Handle(new RequestGuardianCodeCommand(LoginWorld.KurumA, phone), world.Ct);

        foreach (var phone in new[] { LoginWorld.AysePhone, "0533 000 00 00" })
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            (await handler(phone)).ShouldBe(RequestCodeStatus.Accepted);
            watch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(190));
        }
    }
}
