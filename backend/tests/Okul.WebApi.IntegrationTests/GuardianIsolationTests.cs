using System.Net;
using System.Net.Http.Json;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.GuardianSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>
/// Spec 0007 — AC-6. Sensitive areas: Kimlik ve yetki, Tenant izolasyonu. A veli session reaches no staff endpoint, a staff
/// session no veli endpoint (plan 0007, R-3); the same telefon in two kurumlar is two veliler. The kurum's own info
/// (<c>/institution/me</c>) stays open to every role, the veli included (spec 0001; InstitutionEndpointTests).
/// </summary>
[Trait("Category", "Integration")]
public sealed class GuardianIsolationTests
{
    private readonly ApiFixture _api;

    public GuardianIsolationTests(ApiFixture api)
    {
        _api = api;
    }

    [Theory]
    [InlineData("GET", "/api/v1/auth/two-factor")]
    [InlineData("POST", "/api/v1/auth/password")]
    public async Task VeliSession_IsForbidden_OnStaffEndpoints(string method, string route)
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var (client, _) = await _api.LoginGuardianMobileAsync(InstitutionSampleData.WithCampusesCode, phone);

        var response = method == "GET"
            ? await client.GetAsync(route, TestContext.Current.CancellationToken)
            : await client.PostAsJsonAsync(route, new ChangePasswordRequest("a", "b", "b"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(ConsentRoute)]
    [InlineData(MeRoute)]
    public async Task StaffSession_IsForbidden_OnVeliEndpoints(string route)
    {
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, StaffRoles.Teacher);
        var (staff, _) = await _api.LoginMobileAsync(account);

        (await staff.GetAsync(route, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TheSameTelefonInTwoKurumlar_IsTwoVeliler_AndACodeOfOneDoesNotOpenTheOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId, name: "A Velisi");
        await _api.RegisterGuardianAsync(InstitutionSampleData.WithoutCampusesId, phone, name: "B Velisi");
        var client = _api.Testing.CreateClient();

        var codeA = await client.RequestCodeAsync(_api.Testing, InstitutionSampleData.WithCampusesCode, phone);
        (await client.VerifyAsync(InstitutionSampleData.WithoutCampusesCode, phone, codeA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var (inA, _) = await _api.LoginGuardianMobileAsync(InstitutionSampleData.WithCampusesCode, phone);
        var (inB, _) = await _api.LoginGuardianMobileAsync(InstitutionSampleData.WithoutCampusesCode, phone);

        (await inA.GetFromJsonAsync<GuardianProfileResponse>(MeRoute, ct))!.DisplayName.ShouldBe("A Velisi");
        (await inB.GetFromJsonAsync<GuardianProfileResponse>(MeRoute, ct))!.DisplayName.ShouldBe("B Velisi");
    }
}
