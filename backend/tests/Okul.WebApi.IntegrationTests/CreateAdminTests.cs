using System.Net;
using System.Net.Http.Json;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.Hosting;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0003, Constraints — the first administrator (plan 0003, R8): password from stdin, never printed.</summary>
[Trait("Category", "Integration")]
public sealed class CreateAdminTests
{
    private readonly ApiFixture _api;

    public CreateAdminTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task CreatesAnAdministrator_WhoCanLogIn_AndNeverEchoesThePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var email = $"ilk-yonetici-{Guid.NewGuid():N}@okul.test";
        const string password = "ilk-yonetici-sifresi-1";
        string[] args = [CreateAdminCommand.Name, "--kurum-kodu", "bilge", "--e-posta", email];
        var output = new StringWriter();

        (await CreateAdminCommand.RunAsync(_api.Testing.Services, args, new StringReader(password), output)).ShouldBe(0);
        (await CreateAdminCommand.RunAsync(_api.Testing.Services, args, new StringReader(password), output)).ShouldBe(1);
        (await CreateAdminCommand.RunAsync(_api.Testing.Services, args, new StringReader("kisa"), output)).ShouldBe(2);
        (await CreateAdminCommand.RunAsync(_api.Testing.Services,
            [CreateAdminCommand.Name, "--kurum-kodu", "YOK-KOD", "--e-posta", email], new StringReader(password), output)).ShouldBe(1);
        output.ToString().ShouldNotContain(password);

        var login = await _api.Testing.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(InstitutionSampleData.WithoutCampusesCode, email, password, "mobile"), ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await login.Content.ReadFromJsonAsync<SessionResponse>(ct))!.Roles.ShouldBe(["Administrator"]);
    }
}
