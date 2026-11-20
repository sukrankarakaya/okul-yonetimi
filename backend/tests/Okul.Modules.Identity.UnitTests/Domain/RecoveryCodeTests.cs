using System.Text.RegularExpressions;
using Okul.Modules.Identity.Domain;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Domain;

/// <summary>Spec 0004, Requirement 3 / AC-6 — the kurtarma kodu format and how input is read. Pure, fast.</summary>
public sealed partial class RecoveryCodeTests
{
    [Fact]
    public void NewSet_IsTenDistinctCodes_InTheFormat_WithoutLookAlikes()
    {
        var codes = RecoveryCodeFormat.NewSet();

        codes.Count.ShouldBe(10);
        codes.Distinct(StringComparer.Ordinal).Count().ShouldBe(10);
        codes.ShouldAllBe(code => Format().IsMatch(code));
        codes.ShouldAllBe(code => !code.Any(c => "0O1IL".Contains(c)));
        RecoveryCodeFormat.NewSet().ShouldNotBe(codes);
    }

    [Theory]
    [InlineData("K7Q2-9XMD", "K7Q29XMD")]
    [InlineData("k7q2-9xmd", "K7Q29XMD")]
    [InlineData(" k7q2 9xmd ", "K7Q29XMD")]
    [InlineData("K7Q29XMD", "K7Q29XMD")]
    [InlineData("K7Q2-9XM", null)]
    [InlineData("K7Q2-9XMDD", null)]
    [InlineData("K0Q2-9XMD", null)]
    [InlineData("KIQ2-9XMD", null)]
    [InlineData("kıq2-9xmd", null)]
    [InlineData(null, null)]
    public void Normalize_IgnoresCaseDashesAndSpaces_RejectsOtherCharacters(string? input, string? expected)
    {
        RecoveryCodeFormat.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void A_Code_IsUsedOnce()
    {
        var code = RecoveryCode.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "h");
        var now = DateTimeOffset.UtcNow;

        code.TryUse(now).ShouldBeTrue();
        code.IsUnused.ShouldBeFalse();
        code.TryUse(now.AddMinutes(1)).ShouldBeFalse();
        code.UsedAt.ShouldBe(now);
    }

    [GeneratedRegex("^[2-9A-HJKMNP-Z]{4}-[2-9A-HJKMNP-Z]{4}$")]
    private static partial Regex Format();
}
