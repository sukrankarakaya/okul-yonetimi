using System.Net;
using System.Net.Http.Json;
using Okul.Modules.Institution.Contracts;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class InstitutionEndpointTests
{
    private const string MineRoute = "/api/v1/institution/me";
    private readonly ApiFixture _api;

    public InstitutionEndpointTests(ApiFixture api)
    {
        _api = api;
    }

    public static TheoryData<string> AllRoles() => new(Roles.All);

    /// <summary>AC-1</summary>
    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task GetMine_ReturnsOwnInstitutionAndCampuses_ForEachRole(string role)
    {
        var client = await _api.ClientForAsync(InstitutionSampleData.WithCampusesId, role);

        var response = await client.GetAsync(MineRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var institution = await response.Content.ReadFromJsonAsync<InstitutionDto>(TestContext.Current.CancellationToken);
        institution.ShouldNotBeNull();
        institution.Id.ShouldBe(InstitutionSampleData.WithCampusesId);
        institution.Name.ShouldBe(InstitutionSampleData.WithCampusesName);
        institution.Campuses.Select(c => c.Name).ShouldBe(InstitutionSampleData.CampusNamesInsertOrder, ignoreOrder: true);
    }

    /// <summary>AC-7 — only a Turkish collation yields this order (ordinal or root collation would not).</summary>
    [Fact]
    public async Task Campuses_AreOrderedTurkishAlphabetically()
    {
        var client = await _api.ClientForAsync(InstitutionSampleData.WithCampusesId, Roles.Administrator);

        var institution = await client.GetFromJsonAsync<InstitutionDto>(MineRoute, TestContext.Current.CancellationToken);

        institution.ShouldNotBeNull();
        institution.Campuses.Select(c => c.Name).ShouldBe(InstitutionSampleData.CampusNamesTurkishOrder);
    }

    /// <summary>Empty state data for AC-8/AC-9: a kurum without campuses returns an empty list.</summary>
    [Fact]
    public async Task GetMine_InstitutionWithoutCampuses_ReturnsEmptyCampusList()
    {
        var client = await _api.ClientForAsync(InstitutionSampleData.WithoutCampusesId, Roles.Guardian);

        var institution = await client.GetFromJsonAsync<InstitutionDto>(MineRoute, TestContext.Current.CancellationToken);

        institution.ShouldNotBeNull();
        institution.Name.ShouldBe(InstitutionSampleData.WithoutCampusesName);
        institution.Campuses.ShouldBeEmpty();
    }
}
