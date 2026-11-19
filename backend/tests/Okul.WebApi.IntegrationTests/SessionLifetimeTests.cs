using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0003 — Requirement 5 / AC-5, end to end on a fake clock (plan 0003, R6).</summary>
[Trait("Category", "Integration")]
public sealed class SessionLifetimeTests
{
    private const string MineRoute = "/api/v1/institution/me";
    private const string RefreshRoute = "/api/v1/auth/refresh";
    private readonly ApiFixture _api;

    public SessionLifetimeTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task Refresh_After15Minutes_IsSilent()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var web = await _api.LoginWebAsync(IdentitySampleData.Teacher, factory);
        var (mobile, session) = await _api.LoginMobileAsync(IdentitySampleData.Teacher, factory);

        clock.Advance(TimeSpan.FromMinutes(16));
        (await web.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await mobile.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await web.PostAsync(RefreshRoute, null, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await web.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var renewed = await (await factory.CreateClient().PostAsJsonAsync(RefreshRoute, new RefreshRequest(session.RefreshToken), ct))
            .Content.ReadFromJsonAsync<SessionResponse>(ct);
        renewed!.RefreshToken.ShouldNotBe(session.RefreshToken);
        mobile.DefaultRequestHeaders.Authorization = new("Bearer", renewed.AccessToken);
        (await mobile.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("web", 12 * 60)]
    [InlineData("mobile", 30 * 24 * 60)]
    public async Task AbsoluteLimit_Web12h_Mobile30d(string client, int limitMinutes)
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var web = _api.WebClient(factory);
        var login = await web.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            ApiFixture.CodeOf(IdentitySampleData.Teacher), IdentitySampleData.Teacher.Email, _api.SamplePassword, client), ct);
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>(ct))!;
        session.SessionExpiresAt.ShouldBe(clock.GetUtcNow().AddMinutes(limitMinutes), TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromMinutes(limitMinutes - 1));
        (await Refresh(web, client, session, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        clock.Advance(TimeSpan.FromMinutes(2));
        var ended = await Refresh(web, client, session, ct);
        ended.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ended.Content.ReadFromJsonAsync<ProblemDetails>(ct))!.Detail.ShouldBe("Oturumunuzun süresi doldu, tekrar giriş yapın.");
    }

    [Fact]
    public async Task ReusedRefresh_ClosesSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var (mobile, session) = await _api.LoginMobileAsync(IdentitySampleData.Teacher, factory);
        var anonymous = factory.CreateClient();

        var renewed = await (await anonymous.PostAsJsonAsync(RefreshRoute, new RefreshRequest(session.RefreshToken), ct))
            .Content.ReadFromJsonAsync<SessionResponse>(ct);
        clock.Advance(TimeSpan.FromSeconds(11));

        (await anonymous.PostAsJsonAsync(RefreshRoute, new RefreshRequest(session.RefreshToken), ct)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync(RefreshRoute, new RefreshRequest(renewed!.RefreshToken), ct)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
        mobile.DefaultRequestHeaders.Authorization = new("Bearer", renewed.AccessToken);
        (await mobile.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // closed at once
    }

    [Fact]
    public async Task ParallelRefresh_WithinGrace_DoesNotClose()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var (_, session) = await _api.LoginMobileAsync(IdentitySampleData.Teacher, factory);
        var anonymous = factory.CreateClient();

        var first = await anonymous.PostAsJsonAsync(RefreshRoute, new RefreshRequest(session.RefreshToken), ct);
        var second = await anonymous.PostAsJsonAsync(RefreshRoute, new RefreshRequest(session.RefreshToken), ct);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var renewed = await first.Content.ReadFromJsonAsync<SessionResponse>(ct);
        (await anonymous.PostAsJsonAsync(RefreshRoute, new RefreshRequest(renewed!.RefreshToken), ct)).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient web, string client, SessionResponse session, CancellationToken ct) =>
        client == "web"
            ? web.PostAsync(RefreshRoute, null, ct)
            : web.PostAsJsonAsync(RefreshRoute, new RefreshRequest(session.RefreshToken), ct);
}
