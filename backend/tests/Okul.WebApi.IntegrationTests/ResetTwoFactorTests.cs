using System.Net;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Hosting;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.TwoFactorSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0004 — the operations reset (Requirement 9): AC-10. Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class ResetTwoFactorTests
{
    private readonly ApiFixture _api;

    public ResetTwoFactorTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task Reset_ClosesSessions_AndRequiresSetup()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var (mobile, _) = await _api.LoginMobileAsync(admin);
        var web = await _api.LoginWebAsync(admin);
        var open = _api.Testing.CreateClient();
        var pending = await StepAsync(open, _api, admin, "verify"); // a login step open at the reset
        var unused = await _api.AddRecoveryCodeAsync(admin);
        var output = new StringWriter();

        (await Run(admin.Email, output)).ShouldBe(0);

        output.ToString().Trim().ShouldBe("İki adımlı doğrulama sıfırlandı. Kapanan oturum: 2.");
        (await mobile.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await web.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // The open step and the unused kurtarma kodu are gone (review round 1 L-2); the next login sets up again.
        await ShouldAnswerAsync(await VerifyAsync(open, pending.Step, null, unused), HttpStatusCode.Unauthorized, Auth.AuthEndpoints.StepEndedMessage);
        await StepAsync(_api.Testing.CreateClient(), _api, admin, "setup");
    }

    [Fact]
    public async Task Output_HasNoSecret()
    {
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var output = new StringWriter();

        await Run(admin.Email, output);

        var text = output.ToString();
        text.ShouldNotContain(Modules.Identity.Domain.Totp.ToBase32(_api.TwoFactorSecret));
        text.ShouldNotContain(_api.SamplePassword);
        text.ShouldNotContain("v1:");
    }

    [Fact]
    public async Task UnknownAccount_FailsAndChangesNothing()
    {
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Administrator);
        var output = new StringWriter();

        (await Run($"yok-{Guid.NewGuid():N}@okul.test", output)).ShouldBe(1);
        (await ResetTwoFactorCommand.RunAsync(_api.Testing.Services,
            [ResetTwoFactorCommand.Name, "--kurum-kodu", "YOK-KOD", "--e-posta", admin.Email], output)).ShouldBe(1);
        (await ResetTwoFactorCommand.RunAsync(_api.Testing.Services, [ResetTwoFactorCommand.Name], output)).ShouldBe(2);

        output.ToString().ShouldContain("Bu kurumda bu e-posta ile bir hesap yok.");
        await StepAsync(_api.Testing.CreateClient(), _api, admin, "verify"); // still set up
    }

    private Task<int> Run(string email, TextWriter output) =>
        ResetTwoFactorCommand.RunAsync(_api.Testing.Services,
            [ResetTwoFactorCommand.Name, "--kurum-kodu", InstitutionSampleData.WithCampusesCode, "--e-posta", email], output);
}
