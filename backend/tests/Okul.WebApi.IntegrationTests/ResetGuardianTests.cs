using System.Net;
using System.Net.Http.Json;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.Hosting;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.GuardianSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0007 — AC-7: the account is made at the first login; <c>reset-veli</c> closes sessions and lifts the lock.</summary>
[Trait("Category", "Integration")]
public sealed class ResetGuardianTests
{
    private const string Kurum = InstitutionSampleData.WithCampusesCode;
    private readonly ApiFixture _api;

    public ResetGuardianTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task Reset_ClosesSessions_LiftsTheLock_AndPrintsNothingSecret()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var (veli, session) = await _api.LoginGuardianMobileAsync(Kurum, phone);
        var client = _api.Testing.CreateClient();
        var code = await client.RequestCodeAsync(_api.Testing, Kurum, phone);
        for (var i = 0; i < 5; i++)
        {
            await client.VerifyAsync(Kurum, phone, code == "000000" ? "111111" : "000000");
        }

        (await client.VerifyAsync(Kurum, phone, code)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        var output = new StringWriter();
        (await ResetGuardianCommand.RunAsync(_api.Testing.Services, ["reset-veli", "--kurum-kodu", Kurum, "--telefon", phone], output))
            .ShouldBe(0);

        output.ToString().ShouldContain("Kapanan oturum: 1.");
        output.ToString().ShouldNotContain(E164(phone)[3..]);
        (await veli.GetAsync(ConsentRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(session.Session.RefreshToken), ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await _api.LoginGuardianMobileAsync(Kurum, phone); // unlocked: a new code opens a session again
    }

    [Fact]
    public async Task UnknownVeli_IsAClearError()
    {
        var output = new StringWriter();

        (await ResetGuardianCommand.RunAsync(_api.Testing.Services, ["reset-veli", "--kurum-kodu", Kurum, "--telefon", RandomPhone()], output))
            .ShouldBe(1);
        (await ResetGuardianCommand.RunAsync(_api.Testing.Services, ["reset-veli", "--kurum-kodu", Kurum], output)).ShouldBe(2);

        output.ToString().ShouldContain("Bu kurumda bu telefonla kayıtlı bir veli yok.");
    }

    [Fact]
    public async Task FirstLogin_CreatesTheAccount_AndTheNextLoginReusesIt()
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var (_, first) = await _api.LoginGuardianMobileAsync(Kurum, phone);
        var (second, again) = await _api.LoginGuardianMobileAsync(Kurum, phone, consent: false);

        again.ConsentRequired.ShouldBeFalse(); // the same veli: the rıza of the first login still counts
        (await second.GetAsync(MeRoute, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        first.Session.Roles.ShouldBe([Roles.Guardian]);
    }
}
