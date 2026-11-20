using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DevAuthTests
{
    private readonly ApiFixture _api;

    public DevAuthTests(ApiFixture api)
    {
        _api = api;
    }

    /// <summary>AC-6 — in Production the test login does not exist: no route, no token, no session.</summary>
    [Fact]
    public async Task TestLogin_IsNotAvailable_InProduction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var production = _api.CreateFactory("Production");
        var client = production.CreateClient();

        // The route is not merely guarded — it is not mapped at all (and it is in Testing, so the check bites).
        EndpointNames(production).ShouldNotContain(DevTestLoginName);
        EndpointNames(_api.Testing).ShouldContain(DevTestLoginName);

        var login = await client.PostAsJsonAsync(
            DevAuthEndpoints.Route,
            new TestLoginRequest(InstitutionSampleData.WithCampusesId, Roles.Administrator),
            ct);

        // Unmapped + default-deny fallback policy → 401 (not 404); either way no token is issued.
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.Content.ReadAsStringAsync(ct)).ShouldNotContain("accessToken");

        // The production app itself is up and still guarded: the data endpoint needs a session.
        var data = await client.GetAsync("/api/v1/institution/me", ct);
        data.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Spec 0003, AC-9 / B-3 — the published API description never names the test login (so neither the generated
    /// client nor the app bundles can), while the real login is there.
    /// </summary>
    [Fact]
    public async Task OpenApi_HasNoTestLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var development = _api.CreateFactory("Development");

        var document = await development.CreateClient().GetStringAsync("/openapi/v1.json", ct);

        document.ShouldContain("/api/v1/auth/login");
        document.ShouldNotContain("test-login");
        document.ShouldNotContain("DevAuth");
        EndpointNames(development).ShouldContain(DevTestLoginName); // mapped in Development — only not described
    }

    private const string DevTestLoginName = "DevTestLogin";

    private static IEnumerable<string> EndpointNames(OkulApiFactory factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .OfType<string>();
}
