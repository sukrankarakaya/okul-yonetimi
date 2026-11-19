using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Okul.Modules.Institution.Contracts;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Sensitive area: Tenant izolasyonu (BR-1). A = kurum with campuses, B = kurum without.</summary>
[Trait("Category", "Integration")]
public sealed class TenantIsolationTests
{
    private const string MineRoute = "/api/v1/institution/me";
    private static readonly Guid A = InstitutionSampleData.WithCampusesId;
    private static readonly Guid B = InstitutionSampleData.WithoutCampusesId;
    private readonly ApiFixture _api;

    public TenantIsolationTests(ApiFixture api)
    {
        _api = api;
    }

    /// <summary>AC-2 — in both directions.</summary>
    [Fact]
    public async Task GetMine_NeverContainsOtherTenantData()
    {
        var ct = TestContext.Current.CancellationToken;
        var clientA = await _api.ClientForAsync(A, Roles.Teacher);
        var clientB = await _api.ClientForAsync(B, Roles.Teacher);

        var bodyA = await clientA.GetStringAsync(MineRoute, ct);
        var bodyB = await clientB.GetStringAsync(MineRoute, ct);

        bodyA.ShouldNotContain(B.ToString());
        bodyA.ShouldNotContain(InstitutionSampleData.WithoutCampusesName);
        bodyB.ShouldNotContain(A.ToString());
        bodyB.ShouldNotContain(InstitutionSampleData.WithCampusesName);
        foreach (var campusName in InstitutionSampleData.CampusNamesInsertOrder)
        {
            bodyB.ShouldNotContain(campusName);
        }
    }

    /// <summary>AC-3 — query string and header naming another kurum are ignored.</summary>
    [Fact]
    public async Task GetMine_IgnoresClientSuppliedTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var clientB = await _api.ClientForAsync(B, Roles.Administrator);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{MineRoute}?tenantId={A}&tenant_id={A}");
        request.Headers.Add("X-Tenant-Id", A.ToString());

        var response = await clientB.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var institution = await response.Content.ReadFromJsonAsync<InstitutionDto>(ct);
        institution.ShouldNotBeNull();
        institution.Id.ShouldBe(B);
        institution.Campuses.ShouldBeEmpty();
    }

    /// <summary>AC-4 — another kurum's campus and a missing campus get the same "not found".</summary>
    [Fact]
    public async Task GetCampus_OtherTenant_ReturnsSameNotFoundAsMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var clientA = await _api.ClientForAsync(A, Roles.Accountant);
        var clientB = await _api.ClientForAsync(B, Roles.Accountant);
        var campusOfA = (await clientA.GetFromJsonAsync<InstitutionDto>(MineRoute, ct))!.Campuses[0];

        var own = await clientA.GetAsync($"/api/v1/campuses/{campusOfA.Id}", ct);
        var foreign = await clientB.GetAsync($"/api/v1/campuses/{campusOfA.Id}", ct);
        var missing = await clientB.GetAsync($"/api/v1/campuses/{Guid.CreateVersion7()}", ct);

        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var foreignProblem = await foreign.Content.ReadFromJsonAsync<ProblemDetails>(ct);
        var missingProblem = await missing.Content.ReadFromJsonAsync<ProblemDetails>(ct);
        foreignProblem.ShouldNotBeNull();
        missingProblem.ShouldNotBeNull();
        (foreignProblem.Status, foreignProblem.Title, foreignProblem.Detail)
            .ShouldBe((missingProblem.Status, missingProblem.Title, missingProblem.Detail));
        (await foreign.Content.ReadAsStringAsync(ct)).ShouldNotContain(campusOfA.Name);
    }
}
