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

/// <summary>Spec 0004 — mandatory setup at login (Requirements 1–3, 11): AC-1, AC-2, AC-3. Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class TwoFactorSetupTests
{
    private readonly ApiFixture _api;

    public TwoFactorSetupTests(ApiFixture api)
    {
        _api = api;
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Accountant")]
    [InlineData("Teacher", "Accountant")]
    public async Task MandatoryRoles_WithoutTwoFactor_GetSetupStep_NoData(params string[] roles)
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: false, roles);
        var web = _api.WebClient();

        var step = await StepAsync(web, _api, account, "setup", kind: "web");

        (await web.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var bearer = _api.Testing.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", step.Step);
        (await bearer.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // the step is no access token
        (await VerifyAsync(web, step.Step, _api.CodeAt(DateTimeOffset.UtcNow))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_ShowsQrAndKey_ConfirmsWithValidCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: false, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "setup");

        var setup = await StartSetupAsync(client, step.Step);

        setup.Key.ShouldMatch("^[A-Z2-7]{32}$");
        setup.QrCode.ShouldStartWith("data:image/png;base64,");
        Convert.FromBase64String(setup.QrCode["data:image/png;base64,".Length..])[..4].ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        var confirmed = await ConfirmAsync(client, step.Step, ApiFixture.CodeFromKey(setup.Key, clock.GetUtcNow()));
        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var completed = (await confirmed.Content.ReadFromJsonAsync<SetupCompletedResponse>(ct))!;
        UseBearer(client, completed.Session);
        (await client.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // From now on the login asks for the code of THIS key.
        clock.Advance(TimeSpan.FromSeconds(30));
        var next = await StepAsync(factory.CreateClient(), _api, admin, "verify");
        (await VerifyAsync(factory.CreateClient(), next.Step, ApiFixture.CodeFromKey(setup.Key, clock.GetUtcNow()))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongCode_DoesNotConfirm_AndCounts()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: false, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "setup");
        var setup = await StartSetupAsync(client, step.Step);
        var valid = ApiFixture.CodeFromKey(setup.Key, clock.GetUtcNow());
        var wrong = valid == "000000" ? "000001" : "000000";

        for (var i = 0; i < 4; i++)
        {
            await ShouldAnswerAsync(await ConfirmAsync(client, step.Step, wrong), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        }

        // The fifth wrong code locks — setup codes count like login codes (self-critique G1).
        await ShouldAnswerAsync(await ConfirmAsync(client, step.Step, wrong), HttpStatusCode.TooManyRequests, AuthEndpoints.TooManyAttemptsMessage);
        await ShouldAnswerAsync(await ConfirmAsync(client, step.Step, valid), HttpStatusCode.TooManyRequests, AuthEndpoints.TooManyAttemptsMessage);
    }

    [Fact]
    public async Task AbandonedSetup_RestartsWithNewKey()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: false, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var first = await StepAsync(client, _api, admin, "setup");
        var abandoned = await StartSetupAsync(client, first.Step);
        await client.PostAsJsonAsync(CancelRoute, new LoginStepRequest(first.Step), TestContext.Current.CancellationToken);

        var second = await StepAsync(client, _api, admin, "setup"); // still setup: nothing was stored on the account
        var restarted = await StartSetupAsync(client, second.Step);

        restarted.Key.ShouldNotBe(abandoned.Key);
        await ShouldAnswerAsync(await ConfirmAsync(client, first.Step, ApiFixture.CodeFromKey(abandoned.Key, clock.GetUtcNow())),
            HttpStatusCode.Unauthorized, AuthEndpoints.StepEndedMessage);
        (await ConfirmAsync(client, second.Step, ApiFixture.CodeFromKey(restarted.Key, clock.GetUtcNow()))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RecoveryCodes_ShownOnce_NeverAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: false, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "setup");
        var setup = await StartSetupAsync(client, step.Step);

        var confirmed = await ConfirmAsync(client, step.Step, ApiFixture.CodeFromKey(setup.Key, clock.GetUtcNow()));
        var completed = (await confirmed.Content.ReadFromJsonAsync<SetupCompletedResponse>(ct))!;

        confirmed.Headers.CacheControl!.NoStore.ShouldBeTrue();
        completed.RecoveryCodes.Count.ShouldBe(10);
        completed.RecoveryCodes.Distinct().Count().ShouldBe(10);
        completed.RecoveryCodes.ShouldAllBe(c => RecoveryCodeFormat.Normalize(c) != null);
        // The confirm cannot be replayed to see them again; one of them opens a login.
        await ShouldAnswerAsync(await ConfirmAsync(client, step.Step, ApiFixture.CodeFromKey(setup.Key, clock.GetUtcNow())),
            HttpStatusCode.Unauthorized, AuthEndpoints.StepEndedMessage);
        var next = await StepAsync(client, _api, admin, "verify");
        (await VerifyAsync(client, next.Step, null, completed.RecoveryCodes[3])).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
