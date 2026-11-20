using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.GuardianSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0007 — AC-1, AC-2, AC-3 through the real endpoints and database.</summary>
[Trait("Category", "Integration")]
public sealed class GuardianLoginTests
{
    private const string Kurum = InstitutionSampleData.WithCampusesCode;
    private readonly ApiFixture _api;

    public GuardianLoginTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task AC1_RegisteredVeli_GetsACode_AndUnregisteredOrUnknownKurum_GetTheSameAnswer_WithoutSms()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var stranger = RandomPhone();
        var client = _api.Testing.CreateClient();

        var registered = await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest(Kurum, phone), ct);
        var unregistered = await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest(Kurum, stranger), ct);
        var unknownKurum = await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest("YOK-BOYLE", phone), ct);

        foreach (var response in new[] { registered, unregistered, unknownKurum })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<GuardianCodeResponse>(ct))!.Message
                .ShouldBe("Numara kayıtlıysa SMS kodu gönderildi. Kod 5 dakika geçerli.");
        }

        _api.Testing.Sms().LastCodeFor(E164(phone)).ShouldNotBeNull().Length.ShouldBe(6);
        _api.Testing.Sms().LastCodeFor(E164(stranger)).ShouldBeNull();
        _api.Testing.Sms().Sent.Count(s => s.Phone == E164(phone)).ShouldBe(1); // the unknown kurum sent nothing
    }

    [Fact]
    public async Task AC1_TheSameNumberWrittenDifferently_IsTheSameVeli()
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var spelled = "+90 " + phone[1..4] + " " + phone[4..7] + " " + phone[7..9] + " " + phone[9..];
        var client = _api.Testing.CreateClient();

        var code = await client.RequestCodeAsync(_api.Testing, Kurum, spelled);

        (await client.VerifyAsync(Kurum, phone, code)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("0212 123 45 67")]
    [InlineData("12345")]
    [InlineData("")]
    public async Task AC1_InvalidTelefon_IsRefusedWithTheSpecText(string phone)
    {
        var response = await _api.Testing.CreateClient().PostAsJsonAsync(
            CodeRoute, new GuardianCodeRequest(Kurum, phone), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemDetailAsync()).ShouldBe("Lütfen geçerli bir telefon numarası girin.");
    }

    [Fact]
    public async Task AC2_RightCodeOpensASession_UsedOrWrongCodeIsRefused_SpacesAreIgnored()
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var client = _api.Testing.CreateClient();
        var code = await client.RequestCodeAsync(_api.Testing, Kurum, phone);

        var wrong = await client.VerifyAsync(Kurum, phone, code == "000000" ? "111111" : "000000");
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await wrong.ProblemDetailAsync()).ShouldBe("Kod hatalı veya süresi geçmiş.");

        var ok = await client.VerifyAsync(Kurum, phone, $"{code[..3]} {code[3..]}");
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = (await ok.Content.ReadFromJsonAsync<GuardianSessionResponse>(TestContext.Current.CancellationToken))!;
        session.Session.Roles.ShouldBe([Roles.Guardian]);
        session.Session.AccessToken.ShouldNotBeNullOrEmpty();
        session.ConsentRequired.ShouldBeTrue();

        (await client.VerifyAsync(Kurum, phone, code)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AC2_ANewCode_AndVazgec_EndThePendingCode()
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var client = _api.Testing.CreateClient();
        var first = await client.RequestCodeAsync(_api.Testing, Kurum, phone);
        var second = await client.RequestCodeAsync(_api.Testing, Kurum, phone);

        (await client.VerifyAsync(Kurum, phone, first)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync(CancelRoute, new GuardianCodeRequest(Kurum, phone), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.VerifyAsync(Kurum, phone, second)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AC2_AfterFiveMinutes_TheCodeHasExpired()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId, factory: factory);
        var client = factory.CreateClient();
        var code = await client.RequestCodeAsync(factory, Kurum, phone);

        clock.Advance(TimeSpan.FromMinutes(5));
        var expired = await client.VerifyAsync(Kurum, phone, code);

        expired.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await expired.ProblemAsync()).ShouldBe(("code_expired", "Kodun süresi doldu. Yeni kod isteyin."));
    }

    [Fact]
    public async Task AC3_FiveWrongCodesLock_RightCodeRefusedWhileLocked_AndUnregisteredLocksTheSame()
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var stranger = RandomPhone();
        var client = _api.Testing.CreateClient();
        var code = await client.RequestCodeAsync(_api.Testing, Kurum, phone);
        await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest(Kurum, stranger), TestContext.Current.CancellationToken);
        var wrong = code == "000000" ? "111111" : "000000";

        foreach (var who in new[] { phone, stranger })
        {
            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 5; i++)
            {
                statuses.Add((await client.VerifyAsync(Kurum, who, wrong)).StatusCode);
            }

            statuses.ShouldBe([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 4), HttpStatusCode.TooManyRequests]);
        }

        var locked = await client.VerifyAsync(Kurum, phone, code);
        locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await locked.ProblemDetailAsync()).ShouldBe("Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.");
    }

    [Fact]
    public async Task AC3_FourthCodeRequestInAMinute_IsRateLimited_ResendIncluded()
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var client = _api.Testing.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest(Kurum, phone), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var fourth = await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest(Kurum, phone), TestContext.Current.CancellationToken);

        fourth.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await fourth.ProblemDetailAsync()).ShouldBe("Çok sık kod istediniz. 1 dakika sonra tekrar deneyin.");
    }

    [Fact]
    public async Task AC3_TheTwentyFirstAttemptFromOneSource_IsRefused()
    {
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, loginsPerMinute: 20);
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 21; i++)
        {
            statuses.Add((await client.VerifyAsync(Kurum, RandomPhone(), "123456")).StatusCode);
        }

        statuses.Take(20).ShouldAllBe(s => s == HttpStatusCode.Unauthorized);
        statuses[20].ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
