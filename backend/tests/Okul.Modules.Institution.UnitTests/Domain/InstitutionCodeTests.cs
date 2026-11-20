using Okul.Modules.Institution.Domain;
using Shouldly;
using InstitutionEntity = Okul.Modules.Institution.Domain.Institution;

namespace Okul.Modules.Institution.UnitTests.Domain;

/// <summary>Spec 0003, Requirement 1 and AC-3 (case, spaces, Turkish I).</summary>
public sealed class InstitutionCodeTests
{
    [Theory]
    [InlineData("ATATURK-IST", "ATATURK-IST")]
    [InlineData("  ataturk-ist  ", "ATATURK-IST")]
    [InlineData("atatürk-ıst", null)]
    [InlineData("ataturk-ıst", "ATATURK-IST")]
    [InlineData("ATATURK-İST", "ATATURK-IST")]
    [InlineData("ataturk-ist", "ATATURK-IST")]
    [InlineData("abc", "ABC")]
    [InlineData("A1-2B3C4D5E6", "A1-2B3C4D5E6")]
    public void Normalize_IsCaseSpaceAndTurkishIInsensitive(string input, string? expected)
    {
        InstitutionCode.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("ABCDEFGHIJKLM")]
    [InlineData("OKUL 1")]
    [InlineData("OKUL_1")]
    [InlineData("OKUL.1")]
    [InlineData("ŞİŞLİ")]
    public void Normalize_RejectsWhatCanNeverBeACode(string? input)
    {
        InstitutionCode.Normalize(input).ShouldBeNull();
    }

    [Fact]
    public void Create_StoresTheNormalizedCode_AndRejectsAnInvalidOne()
    {
        InstitutionEntity.Create(Guid.CreateVersion7(), "Atatürk Koleji", " ataturk-ıst ").Code.ShouldBe("ATATURK-IST");
        Should.Throw<ArgumentException>(() => InstitutionEntity.Create(Guid.CreateVersion7(), "Atatürk Koleji", "x"));
    }
}
