using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.TwoFactorSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0004 — Güvenlik (Requirements 7, 8): AC-8, AC-9, AC-12. Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class TwoFactorSettingsTests
{
    private const string StatusRoute = "/api/v1/auth/two-factor";
    private readonly ApiFixture _api;

    public TwoFactorSettingsTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task Teacher_EnablesAndDisablesWithCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var teacher = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Teacher);
        var (client, _) = await _api.LoginMobileAsync(teacher, factory);
        (await Status(client)).ShouldBe(new TwoFactorStatusResponse(false, false, false, 0));

        var enable = await (await client.PostAsync(StatusRoute + "/enable", null, ct)).Content.ReadFromJsonAsync<TwoFactorEnableResponse>(ct);
        enable!.QrCode.ShouldStartWith("data:image/png;base64,");
        var confirmed = await client.PostAsJsonAsync(StatusRoute + "/enable/confirm",
            new VerifyLoginRequest(enable.Step, ApiFixture.CodeFromKey(enable.Key, clock.GetUtcNow()), null), ct);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await confirmed.Content.ReadFromJsonAsync<RecoveryCodesResponse>(ct))!.RecoveryCodes.Count.ShouldBe(10);
        (await Status(client)).ShouldBe(new TwoFactorStatusResponse(true, false, true, 10));
        await StepAsync(factory.CreateClient(), _api, teacher, "verify"); // the login now asks for a code

        clock.Advance(TimeSpan.FromSeconds(30));
        var disabled = await client.PostAsJsonAsync(StatusRoute + "/disable",
            new TwoFactorCodeRequest(ApiFixture.CodeFromKey(enable.Key, clock.GetUtcNow())), ct);

        disabled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Status(client)).ShouldBe(new TwoFactorStatusResponse(false, false, false, 0));
        (await PasswordAsync(factory.CreateClient(), _api, teacher)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// Review round 1 M-1 — the Güvenlik "aç" step is refused by the anonymous login setup: it must not reset the login
    /// count (B-11 would be bypassed: wrong current passwords, enable, confirm at the login, disable, again).
    /// </summary>
    [Fact]
    public async Task EnableStep_IsRefusedAtTheLoginSetup_AndDoesNotResetTheCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var teacher = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Teacher);
        var (client, _) = await _api.LoginMobileAsync(teacher, factory);
        for (var i = 0; i < 4; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/auth/password", new ChangePasswordRequest("yanlis-sifre-123", "yepyeni-sifre-2026", "yepyeni-sifre-2026"), ct))
                .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var enable = (await (await client.PostAsync(StatusRoute + "/enable", null, ct)).Content.ReadFromJsonAsync<TwoFactorEnableResponse>(ct))!;
        var code = ApiFixture.CodeFromKey(enable.Key, clock.GetUtcNow());
        await ShouldAnswerAsync(await ConfirmAsync(factory.CreateClient(), enable.Step, code), HttpStatusCode.Unauthorized, AuthEndpoints.StepEndedMessage);
        await ShouldAnswerAsync(await factory.CreateClient().PostAsJsonAsync(SetupRoute, new LoginStepRequest(enable.Step), ct),
            HttpStatusCode.Unauthorized, AuthEndpoints.StepEndedMessage);

        // The count still stands at 4: the fifth wrong current password locks.
        (await client.PostAsJsonAsync("/api/v1/auth/password", new ChangePasswordRequest("yanlis-sifre-123", "yepyeni-sifre-2026", "yepyeni-sifre-2026"), ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/auth/password", new ChangePasswordRequest(_api.SamplePassword, "yepyeni-sifre-2026", "yepyeni-sifre-2026"), ct))
            .StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>Review round 1 M-1 — a login setup step is refused by the signed-in enable confirm too, and an "on" account cannot set up again.</summary>
    [Fact]
    public async Task EnableConfirm_RefusesALoginStep_AndAnAccountAlreadyOn()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var teacher = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: true, StaffRoles.Teacher);
        var client = await SignedInAsync(factory, clock, teacher);

        (await client.PostAsync(StatusRoute + "/enable", null, ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldAnswerAsync(await client.PostAsJsonAsync(StatusRoute + "/enable/confirm",
            new VerifyLoginRequest((await StepAsync(factory.CreateClient(), _api, teacher, "verify")).Step, _api.CodeAt(clock.GetUtcNow()), null), ct),
            HttpStatusCode.BadRequest, AuthEndpoints.StepEndedMessage);
    }

    /// <summary>Review round 2 L-1 — two "aç" steps: once one is confirmed, the other is refused and the first codes stay.</summary>
    [Fact]
    public async Task OlderEnableStep_AfterAConfirmedOne_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var teacher = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Teacher);
        var (client, _) = await _api.LoginMobileAsync(teacher, factory);
        var a = (await (await client.PostAsync(StatusRoute + "/enable", null, ct)).Content.ReadFromJsonAsync<TwoFactorEnableResponse>(ct))!;
        var b = (await (await client.PostAsync(StatusRoute + "/enable", null, ct)).Content.ReadFromJsonAsync<TwoFactorEnableResponse>(ct))!;

        var confirmed = await client.PostAsJsonAsync(StatusRoute + "/enable/confirm",
            new VerifyLoginRequest(a.Step, ApiFixture.CodeFromKey(a.Key, clock.GetUtcNow()), null), ct);
        var codes = (await confirmed.Content.ReadFromJsonAsync<RecoveryCodesResponse>(ct))!.RecoveryCodes;

        await ShouldAnswerAsync(await client.PostAsJsonAsync(StatusRoute + "/enable/confirm",
            new VerifyLoginRequest(b.Step, ApiFixture.CodeFromKey(b.Key, clock.GetUtcNow()), null), ct),
            HttpStatusCode.BadRequest, AuthEndpoints.StepEndedMessage);
        var login = factory.CreateClient();
        (await VerifyAsync(login, (await StepAsync(login, _api, teacher, "verify")).Step, null, codes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WrongCode_DoesNotDisable()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var teacher = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: true, StaffRoles.Teacher);
        var client = await SignedInAsync(factory, clock, teacher);

        await ShouldAnswerAsync(await client.PostAsJsonAsync(StatusRoute + "/disable", new TwoFactorCodeRequest(Wrong(clock)),
            TestContext.Current.CancellationToken), HttpStatusCode.BadRequest, AuthEndpoints.InvalidCodeMessage);

        (await Status(client)).Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Mandatory_CannotDisable()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var accountant = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Teacher, StaffRoles.Accountant);
        var client = await SignedInAsync(factory, clock, accountant);
        (await Status(client)).ShouldBe(new TwoFactorStatusResponse(true, true, false, 0));

        clock.Advance(TimeSpan.FromSeconds(30));
        await ShouldAnswerAsync(await client.PostAsJsonAsync(StatusRoute + "/disable", new TwoFactorCodeRequest(_api.CodeAt(clock.GetUtcNow())),
            TestContext.Current.CancellationToken), HttpStatusCode.Forbidden, AuthEndpoints.TwoFactorMandatoryMessage);
        (await Status(client)).Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task RenewRecoveryCodes_OldOnesStopWorking()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var old = await _api.AddRecoveryCodeAsync(admin, factory);
        var client = await SignedInAsync(factory, clock, admin);

        await ShouldAnswerAsync(await client.PostAsJsonAsync(StatusRoute + "/recovery-codes", new TwoFactorCodeRequest(Wrong(clock)), ct),
            HttpStatusCode.BadRequest, AuthEndpoints.InvalidCodeMessage);
        clock.Advance(TimeSpan.FromSeconds(30));
        var renewed = await client.PostAsJsonAsync(StatusRoute + "/recovery-codes", new TwoFactorCodeRequest(_api.CodeAt(clock.GetUtcNow())), ct);

        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var codes = (await renewed.Content.ReadFromJsonAsync<RecoveryCodesResponse>(ct))!.RecoveryCodes;
        codes.Count.ShouldBe(10);
        var login = factory.CreateClient();
        var step = await StepAsync(login, _api, admin, "verify");
        await ShouldAnswerAsync(await VerifyAsync(login, step.Step, null, old), HttpStatusCode.Unauthorized, AuthEndpoints.InvalidCodeMessage);
        (await VerifyAsync(login, step.Step, null, codes[0])).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemainingCount_DecreasesAndResets()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var client = await SignedInAsync(factory, clock, admin);
        clock.Advance(TimeSpan.FromSeconds(30));
        var codes = (await (await client.PostAsJsonAsync(StatusRoute + "/recovery-codes",
            new TwoFactorCodeRequest(_api.CodeAt(clock.GetUtcNow())), ct)).Content.ReadFromJsonAsync<RecoveryCodesResponse>(ct))!.RecoveryCodes;
        (await Status(client)).RemainingRecoveryCodes.ShouldBe(10);

        for (var i = 0; i < 7; i++)
        {
            var login = factory.CreateClient();
            (await VerifyAsync(login, (await StepAsync(login, _api, admin, "verify")).Step, null, codes[i])).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await Status(client)).RemainingRecoveryCodes.ShouldBe(3);
        clock.Advance(TimeSpan.FromSeconds(30));
        await client.PostAsJsonAsync(StatusRoute + "/recovery-codes", new TwoFactorCodeRequest(_api.CodeAt(clock.GetUtcNow())), ct);
        (await Status(client)).RemainingRecoveryCodes.ShouldBe(10);
    }

    private async Task<HttpClient> SignedInAsync(OkulApiFactory factory, FakeTimeProvider clock, IdentitySampleData.SampleAccount account)
    {
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, account, "verify");
        var verified = await VerifyAsync(client, step.Step, _api.CodeAt(clock.GetUtcNow()));
        UseBearer(client, (await verified.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken))!);
        return client;
    }

    private static async Task<TwoFactorStatusResponse> Status(HttpClient client) =>
        (await client.GetFromJsonAsync<TwoFactorStatusResponse>(StatusRoute, TestContext.Current.CancellationToken))!;

    private string Wrong(FakeTimeProvider clock)
    {
        var valid = _api.CodeAt(clock.GetUtcNow());
        return valid == "000000" ? "000001" : "000000";
    }
}
