using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0003 — Requirement 4 / AC-3, end to end on a fake clock. Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class LockoutAndRateLimitTests
{
    private const string LoginRoute = "/api/v1/auth/login";
    private const string Wrong = "yanlis-sifre-123";
    private readonly ApiFixture _api;

    public LockoutAndRateLimitTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task Locks_After5_EvenForUnknownAccount_AndUnlocksAfter15Minutes()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var known = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var unknownEmail = $"yok-{Guid.NewGuid():N}@okul.test";

        for (var i = 0; i < 5; i++)
        {
            (await Login(factory, known.Email, Wrong)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await Login(factory, unknownEmail, Wrong)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await ShouldBeTooMany(await Login(factory, known.Email, _api.SamplePassword));
        await ShouldBeTooMany(await Login(factory, unknownEmail, _api.SamplePassword));

        clock.Advance(TimeSpan.FromMinutes(14));
        await ShouldBeTooMany(await Login(factory, known.Email, Wrong)); // does not extend the lock
        clock.Advance(TimeSpan.FromMinutes(1));
        (await Login(factory, known.Email, _api.SamplePassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetsOnSuccess_And_AfterQuietWindow()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);

        await Fail(factory, account.Email, 4);
        (await Login(factory, account.Email, _api.SamplePassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Fail(factory, account.Email, 4);
        clock.Advance(TimeSpan.FromMinutes(15));
        await Fail(factory, account.Email, 4);

        (await Login(factory, account.Email, _api.SamplePassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Source_21stAttemptInAMinute_IsThrottled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, loginsPerMinute: 20);

        for (var i = 0; i < 20; i++)
        {
            // Different e-mails: the account lock is not what stops attempt 21.
            (await Login(factory, $"kaynak-{i}-{Guid.NewGuid():N}@okul.test", Wrong)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await ShouldBeTooMany(await Login(factory, IdentityAdministratorEmail, _api.SamplePassword));
    }

    private const string IdentityAdministratorEmail = "yonetici@ataturk.test";

    private async Task Fail(OkulApiFactory factory, string email, int times)
    {
        for (var i = 0; i < times; i++)
        {
            (await Login(factory, email, Wrong)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private static Task<HttpResponseMessage> Login(OkulApiFactory factory, string email, string password) =>
        factory.CreateClient().PostAsJsonAsync(
            LoginRoute, new LoginRequest(InstitutionSampleData.WithCampusesCode, email, password, "mobile"),
            TestContext.Current.CancellationToken);

    private static async Task ShouldBeTooMany(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problem!.Detail.ShouldBe("Çok fazla başarısız deneme. 15 dakika sonra tekrar deneyin.");
    }
}
