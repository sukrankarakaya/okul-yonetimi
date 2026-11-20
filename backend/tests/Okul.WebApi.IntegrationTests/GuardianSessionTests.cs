using System.Net;
using System.Net.Http.Json;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.GuardianSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0007 — AC-4 (rıza before data, on the server) and AC-5 (the session rules of spec 0003).</summary>
[Trait("Category", "Integration")]
public sealed class GuardianSessionTests
{
    private const string Kurum = InstitutionSampleData.WithCampusesCode;
    private readonly ApiFixture _api;

    public GuardianSessionTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task AC4_WithoutRiza_NoVeliData_WithRiza_TheProfile_AndTheNextLoginDoesNotAskAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId, name: "Ayşe Yılmaz");
        var (client, session) = await _api.LoginGuardianMobileAsync(Kurum, phone, consent: false);
        session.ConsentRequired.ShouldBeTrue();

        (await client.GetAsync(MeRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var before = (await client.GetFromJsonAsync<GuardianConsentResponse>(ConsentRoute, ct))!;
        before.Accepted.ShouldBeFalse();
        before.TextVersion.ShouldBe("taslak");
        before.Text.ShouldNotBeNullOrWhiteSpace();

        var accepted = await client.PostAsync(ConsentRoute, null, ct);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await accepted.Content.ReadFromJsonAsync<GuardianConsentResponse>(ct))!.Accepted.ShouldBeTrue();

        var me = (await client.GetFromJsonAsync<GuardianProfileResponse>(MeRoute, ct))!;
        me.DisplayName.ShouldBe("Ayşe Yılmaz");
        me.MaskedPhone.ShouldBe($"+90 5** *** ** {phone[^2..]}");

        var (_, again) = await _api.LoginGuardianMobileAsync(Kurum, phone, consent: false);
        again.ConsentRequired.ShouldBeFalse();
    }

    [Fact]
    public async Task AC5_MobileSession_RenewsWithTheVeliRole_AndLogoutClosesItAtOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var (client, session) = await _api.LoginGuardianMobileAsync(Kurum, phone);
        var anonymous = _api.Testing.CreateClient();

        var renewed = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(session.Session.RefreshToken), ct);
        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var next = (await renewed.Content.ReadFromJsonAsync<SessionResponse>(ct))!;
        next.Roles.ShouldBe([Roles.Guardian]);
        next.SessionExpiresAt.ShouldBe(session.Session.SessionExpiresAt);

        (await client.PostAsync("/api/v1/auth/logout", null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync(ConsentRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(next.RefreshToken), ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AC5_ReusedRefreshCredential_ClosesTheVeliSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var (client, session) = await _api.LoginGuardianMobileAsync(Kurum, phone);
        var anonymous = _api.Testing.CreateClient();
        var first = session.Session.RefreshToken;

        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first), ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Task.Delay(TimeSpan.FromSeconds(11), ct); // past the parallel-refresh grace (spec 0003, R5)
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first), ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await client.GetAsync(ConsentRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AC5_WebSession_UsesHttpOnlyCookies_AndNoTokensInTheBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var web = _api.WebClient();
        var code = await web.RequestCodeAsync(_api.Testing, Kurum, phone);

        var response = await web.VerifyAsync(Kurum, phone, code, "web");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = (await response.Content.ReadFromJsonAsync<GuardianSessionResponse>(ct))!;
        body.Session.AccessToken.ShouldBeNull();
        body.Session.RefreshToken.ShouldBeNull();
        response.Headers.GetValues("Set-Cookie").ShouldAllBe(c => c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        (await web.GetAsync(ConsentRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
