using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.TwoFactorSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0004 — the code step of the login (Requirements 4–5, 3): AC-4, AC-5, AC-6, AC-1 (Öğretmen). Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class TwoFactorLoginTests
{
    private readonly ApiFixture _api;

    public TwoFactorLoginTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task TeacherWithoutTwoFactor_GetsSessionDirectly()
    {
        var teacher = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Teacher);

        var response = await PasswordAsync(_api.Testing.CreateClient(), _api, teacher);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken))!.AccessToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidCode_OpensSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();

        var step = await StepAsync(client, _api, admin, "verify");
        (await client.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // the step opens no data
        var verified = await VerifyAsync(client, step.Step, _api.CodeAt(clock.GetUtcNow()));

        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
        UseBearer(client, (await verified.Content.ReadFromJsonAsync<SessionResponse>(ct))!);
        (await client.GetFromJsonAsync<InstitutionDto>(MineRoute, ct))!.Id.ShouldBe(InstitutionSampleData.WithCampusesId);
    }

    [Fact]
    public async Task NoOrWrongCode_NoSession()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Accountant);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "verify");
        var wrong = ((int.Parse(_api.CodeAt(clock.GetUtcNow()), System.Globalization.CultureInfo.InvariantCulture) + 1) % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

        await ShouldAnswerAsync(await VerifyAsync(client, step.Step, null), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        await ShouldAnswerAsync(await VerifyAsync(client, step.Step, wrong), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        await ShouldAnswerAsync(await VerifyAsync(client, "x.y.z", _api.CodeAt(clock.GetUtcNow())), HttpStatusCode.Unauthorized, AuthEndpoints.StepEndedMessage);
        (await VerifyAsync(client, step.Step, _api.CodeAt(clock.GetUtcNow()))).StatusCode.ShouldBe(HttpStatusCode.OK); // the step survived the wrong ones
    }

    [Fact]
    public async Task StepExpiresAfter5Minutes()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "verify");

        clock.Advance(TimeSpan.FromMinutes(5));

        await ShouldAnswerAsync(await VerifyAsync(client, step.Step, _api.CodeAt(clock.GetUtcNow())),
            HttpStatusCode.Unauthorized, "Doğrulama süresi doldu, tekrar giriş yapın.");
    }

    [Fact]
    public async Task Cancel_EndsStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "verify");

        (await client.PostAsJsonAsync(CancelRoute, new LoginStepRequest(step.Step), ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ShouldAnswerAsync(await VerifyAsync(client, step.Step, _api.CodeAt(clock.GetUtcNow())),
            HttpStatusCode.Unauthorized, AuthEndpoints.StepEndedMessage);
    }

    [Fact]
    public async Task Refresh_DoesNotAskCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "verify");
        var session = (await (await VerifyAsync(client, step.Step, _api.CodeAt(clock.GetUtcNow()))).Content.ReadFromJsonAsync<SessionResponse>(ct))!;

        clock.Advance(TimeSpan.FromMinutes(16));
        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(session.RefreshToken), ct);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await refreshed.Content.ReadFromJsonAsync<SessionResponse>(ct))!.AccessToken.ShouldNotBeNull();
    }

    [Fact]
    public async Task AcceptedCode_IsRefusedTheSecondTime()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var code = _api.CodeAt(clock.GetUtcNow());
        (await VerifyAsync(client, (await StepAsync(client, _api, admin, "verify")).Step, code)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await StepAsync(client, _api, admin, "verify");

        await ShouldAnswerAsync(await VerifyAsync(client, again.Step, code), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        clock.Advance(TimeSpan.FromSeconds(30));
        (await VerifyAsync(client, again.Step, _api.CodeAt(clock.GetUtcNow()))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PreviousAndNextWindow_Accepted_TwoAway_Refused()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var now = clock.GetUtcNow();

        var first = await StepAsync(client, _api, admin, "verify");
        await ShouldAnswerAsync(await VerifyAsync(client, first.Step, _api.CodeAt(now.AddSeconds(-60))), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        await ShouldAnswerAsync(await VerifyAsync(client, first.Step, _api.CodeAt(now.AddSeconds(60))), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        (await VerifyAsync(client, first.Step, _api.CodeAt(now.AddSeconds(-30)))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await VerifyAsync(client, (await StepAsync(client, _api, admin, "verify")).Step, _api.CodeAt(now.AddSeconds(30))))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SpacedCode_IsAccepted()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var code = _api.CodeAt(clock.GetUtcNow());

        (await VerifyAsync(client, (await StepAsync(client, _api, admin, "verify")).Step, " " + code[..3] + " " + code[3..] + " "))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ParallelSameCode_OnlyOneSession()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var steps = new List<(HttpClient Client, string Step)>();
        for (var i = 0; i < 6; i++)
        {
            var client = factory.CreateClient();
            steps.Add((client, (await StepAsync(client, _api, admin, "verify")).Step));
        }

        var code = _api.CodeAt(clock.GetUtcNow());
        var answers = await Task.WhenAll(steps.Select(s => VerifyAsync(s.Client, s.Step, code)));

        // One session; every other request is refused — as a wrong code (401), or locked (429) once the five refusals
        // reach the login lock (Requirement 6).
        answers.Count(a => a.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        answers.Where(a => a.StatusCode != HttpStatusCode.OK)
            .ShouldAllBe(a => a.StatusCode == HttpStatusCode.Unauthorized || a.StatusCode == HttpStatusCode.TooManyRequests);
    }

    /// <summary>Review round 1 M-2 — one kurtarma kodu in parallel requests (separate steps) opens one session.</summary>
    [Fact]
    public async Task ParallelSameRecoveryCode_OnlyOneSession()
    {
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var code = await _api.AddRecoveryCodeAsync(admin);
        var steps = new List<(HttpClient Client, string Step)>();
        for (var i = 0; i < 4; i++)
        {
            var client = _api.Testing.CreateClient();
            steps.Add((client, (await StepAsync(client, _api, admin, "verify")).Step));
        }

        var answers = await Task.WhenAll(steps.Select(s => VerifyAsync(s.Client, s.Step, null, code)));

        answers.Count(a => a.StatusCode == HttpStatusCode.OK).ShouldBe(1);
    }

    /// <summary>Review round 1 M-2 — one step with two different kurtarma kodları in parallel opens one session.</summary>
    [Fact]
    public async Task OneStep_TwoParallelRecoveryCodes_OnlyOneSession()
    {
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = _api.Testing.CreateClient();
        var step = (await StepAsync(client, _api, admin, "verify")).Step;
        var codes = new[] { await _api.AddRecoveryCodeAsync(admin), await _api.AddRecoveryCodeAsync(admin), await _api.AddRecoveryCodeAsync(admin) };

        var answers = await Task.WhenAll(codes.Select(c => VerifyAsync(_api.Testing.CreateClient(), step, null, c)));

        answers.Count(a => a.StatusCode == HttpStatusCode.OK).ShouldBe(1);
    }

    [Fact]
    public async Task RecoveryCode_OpensOnce_AnySpelling()
    {
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = _api.Testing.CreateClient();
        var code = await _api.AddRecoveryCodeAsync(admin);
        var spelled = " " + code.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal) + " ";

        (await VerifyAsync(client, (await StepAsync(client, _api, admin, "verify")).Step, null, spelled)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await StepAsync(client, _api, admin, "verify");
        await ShouldAnswerAsync(await VerifyAsync(client, again.Step, null, code), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        await ShouldAnswerAsync(await VerifyAsync(client, again.Step, null, "AAAA-AAAA"), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
    }

    private sealed record InstitutionDto(Guid Id, string Name);
}
