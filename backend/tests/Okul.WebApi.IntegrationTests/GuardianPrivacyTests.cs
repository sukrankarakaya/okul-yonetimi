using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Okul.Modules.Enrollment.Infrastructure.Persistence;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.GuardianSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0007 — AC-8: the code is not readable in the database or any answer; the provider failing is told.</summary>
[Trait("Category", "Integration")]
public sealed class GuardianPrivacyTests
{
    private const string Kurum = InstitutionSampleData.WithCampusesCode;
    private readonly ApiFixture _api;

    public GuardianPrivacyTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task TheDatabase_HoldsNeitherTheCodeNorTheTelefonOfTheLoginState()
    {
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var code = await _api.Testing.CreateClient().RequestCodeAsync(_api.Testing, Kurum, phone);
        var national = E164(phone)[3..];

        await using var scope = _api.Testing.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardianLoginDbContext>();
        var rows = await db.Codes.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        var throttles = await db.Throttles.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(r => !r.CodeHash.Contains(code) && !r.PairKey.Contains(national));
        throttles.ShouldAllBe(t => !t.Key.Contains(national));
    }

    [Fact]
    public async Task Answers_NeverCarryTheCodeOrTheFullTelefon()
    {
        var ct = TestContext.Current.CancellationToken;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId);
        var client = _api.Testing.CreateClient();
        var national = E164(phone)[3..];

        var requested = await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest(Kurum, phone), ct);
        var code = _api.Testing.Sms().LastCodeFor(E164(phone))!;
        var wrong = await client.VerifyAsync(Kurum, phone, code == "000000" ? "111111" : "000000");
        var ok = await client.VerifyAsync(Kurum, phone, code);

        foreach (var response in new[] { requested, wrong, ok })
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            body.ShouldNotContain(code);
            body.ShouldNotContain(national);
        }
    }

    [Fact]
    public async Task ProviderDown_IsToldWithTheSpecText()
    {
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment);
        factory.Sms().Fail = true;
        var phone = await _api.RegisterGuardianAsync(InstitutionSampleData.WithCampusesId, factory: factory);

        var response = await factory.CreateClient().PostAsJsonAsync(
            CodeRoute, new GuardianCodeRequest(Kurum, phone), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.ProblemDetailAsync()).ShouldBe("SMS gönderilemedi. Tekrar deneyin.");
    }
}
