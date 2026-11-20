using Microsoft.Extensions.DependencyInjection;
using Okul.Modules.Institution.Contracts;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>
/// Spec 0003, Requirement 1 / plan 0003 R2 — the pre-session kurum kodu lookup on the real database: it runs with
/// no tenant (no session yet), finds the kurum whatever the case, spaces or Turkish I, and returns only its id.
/// </summary>
[Trait("Category", "Integration")]
public sealed class InstitutionDirectoryTests
{
    private readonly ApiFixture _api;

    public InstitutionDirectoryTests(ApiFixture api)
    {
        _api = api;
    }

    [Theory]
    [InlineData("ATATURK-IST")]
    [InlineData("  ataturk-ist ")]
    [InlineData("ataturk-ıst")]
    [InlineData("ATATURK-İST")]
    public async Task KnownCode_ResolvesToItsKurum_WithoutASession(string code)
    {
        await using var scope = _api.Testing.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IInstitutionDirectory>();

        var id = await directory.FindIdByCodeAsync(code, TestContext.Current.CancellationToken);

        id.ShouldBe(InstitutionSampleData.WithCampusesId);
    }

    [Theory]
    [InlineData("BILGE", true)]
    [InlineData("YOK-BOYLE", false)]
    [InlineData("x", false)]
    [InlineData("ŞİŞLİ", false)]
    [InlineData("", false)]
    public async Task OtherCodes_ResolveOnlyWhenTheyExist(string code, bool exists)
    {
        await using var scope = _api.Testing.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IInstitutionDirectory>();

        var id = await directory.FindIdByCodeAsync(code, TestContext.Current.CancellationToken);

        id.ShouldBe(exists ? InstitutionSampleData.WithoutCampusesId : null);
    }
}
