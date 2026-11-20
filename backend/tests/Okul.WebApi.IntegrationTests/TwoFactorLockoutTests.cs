using System.Net;
using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.TwoFactorSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0004 — wrong codes and the login lock (Requirement 6): AC-7. Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class TwoFactorLockoutTests
{
    private const string WrongPassword = "yanlis-sifre-123";
    private readonly ApiFixture _api;

    public TwoFactorLockoutTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task ThreeWrongPasswords_TwoWrongCodes_Lock()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await PasswordAsync(client, _api, admin, WrongPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var step = await StepAsync(client, _api, admin, "verify"); // the right password does not reset the count
        var wrong = Wrong(clock);
        await ShouldAnswerAsync(await VerifyAsync(client, step.Step, wrong), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);

        await ShouldAnswerAsync(await VerifyAsync(client, step.Step, wrong), HttpStatusCode.TooManyRequests, AuthEndpoints.TooManyAttemptsMessage);
        await ShouldAnswerAsync(await PasswordAsync(client, _api, admin), HttpStatusCode.TooManyRequests, AuthEndpoints.TooManyAttemptsMessage);
    }

    [Fact]
    public async Task LockedRefusesValidCode_UntilWindowEnds()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Accountant);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "verify");
        for (var i = 0; i < 5; i++)
        {
            await VerifyAsync(client, step.Step, Wrong(clock));
        }

        await ShouldAnswerAsync(await VerifyAsync(client, step.Step, _api.CodeAt(clock.GetUtcNow())),
            HttpStatusCode.TooManyRequests, AuthEndpoints.TooManyAttemptsMessage);

        clock.Advance(TimeSpan.FromMinutes(15));
        var after = await StepAsync(client, _api, admin, "verify");
        (await VerifyAsync(client, after.Step, _api.CodeAt(clock.GetUtcNow()))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Source_21stCodeAttempt_IsThrottled()
    {
        // No override: the production source limit (20/min) shared by the password and code steps (self-critique G2).
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, loginsPerMinute: null);
        var client = factory.CreateClient();
        for (var i = 0; i < 20; i++)
        {
            (await VerifyAsync(client, "x.y.z", "123456")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await ShouldAnswerAsync(await VerifyAsync(client, "x.y.z", "123456"), HttpStatusCode.TooManyRequests, AuthEndpoints.TooManyAttemptsMessage);
    }

    private string Wrong(FakeTimeProvider clock)
    {
        var valid = _api.CodeAt(clock.GetUtcNow());
        return valid == "000000" ? "000001" : "000000";
    }
}
