using Okul.Testing.Database;
using Shouldly;

namespace Okul.WebApi.UnitTests.TestDatabase;

/// <summary>Spec 0002, CB-4 — pure, fast profile (no trait).</summary>
public sealed class ServerRequirementsTests
{
    [Theory]
    [InlineData(180000)]
    [InlineData(180001)]
    [InlineData(189999)]
    public void Accepts_AnyPatchOf18_WithIcu(int versionNum)
    {
        ServerRequirements.Problem(versionNum, hasCollation: true).ShouldBeNull();
    }

    [Theory]
    [InlineData(170005, "17")]
    [InlineData(190000, "19")]
    [InlineData(90624, "9")]
    public void RejectsOtherMajor_And_MissingIcu_WithReason(int versionNum, string major)
    {
        ServerRequirements.Problem(versionNum, hasCollation: true).ShouldBe(
            $"Test sunucusu PostgreSQL 18 olmalı; bulunan ana sürüm: {major}.");
        ServerRequirements.Problem(180001, hasCollation: false)!.ShouldContain("tr-TR-x-icu");
    }
}
