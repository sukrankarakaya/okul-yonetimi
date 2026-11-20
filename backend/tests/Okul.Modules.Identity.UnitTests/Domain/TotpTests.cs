using System.Text;
using Okul.Modules.Identity.Domain;
using Shouldly;

namespace Okul.Modules.Identity.UnitTests.Domain;

/// <summary>Spec 0004, Requirement 5 / AC-5 — RFC 6238 (SHA-1) codes, ±1 step of drift, spaces ignored. Pure, fast.</summary>
public sealed class TotpTests
{
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    // RFC 6238, Appendix B (SHA-1); the 8-digit reference values truncated to the 6 digits apps show.
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Compute_MatchesTheRfcVectors(long unixSeconds, string expected)
    {
        Totp.Compute(RfcSecret, Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))).ShouldBe(expected);
    }

    [Fact]
    public void PreviousAndNextStep_AreAccepted_TwoStepsAway_AreRefused()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_015);
        var step = Totp.StepAt(now);

        Totp.MatchingStep(RfcSecret, Totp.Compute(RfcSecret, step), now).ShouldBe(step);
        Totp.MatchingStep(RfcSecret, Totp.Compute(RfcSecret, step - 1), now).ShouldBe(step - 1);
        Totp.MatchingStep(RfcSecret, Totp.Compute(RfcSecret, step + 1), now).ShouldBe(step + 1);
        Totp.MatchingStep(RfcSecret, Totp.Compute(RfcSecret, step - 2), now).ShouldBeNull();
        Totp.MatchingStep(RfcSecret, Totp.Compute(RfcSecret, step + 2), now).ShouldBeNull();
    }

    [Theory]
    [InlineData(" 123 456 ", "123456")]
    [InlineData("123456", "123456")]
    [InlineData("12345", null)]
    [InlineData("1234567", null)]
    [InlineData("12345a", null)]
    [InlineData("١٢٣٤٥٦", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_KeepsSixAsciiDigits_IgnoringSpaces(string? input, string? expected)
    {
        Totp.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void SpacedCode_Matches()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var code = Totp.Compute(RfcSecret, Totp.StepAt(now));

        Totp.MatchingStep(RfcSecret, code[..3] + " " + code[3..], now).ShouldBe(Totp.StepAt(now));
    }

    [Fact]
    public void Base32_AndProvisioningUri_AreWhatAuthenticatorAppsRead()
    {
        Totp.ToBase32(RfcSecret).ShouldBe("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");
        Totp.ProvisioningUri("Okul Yönetim", "ATATURK-IST:yonetici@ataturk.test", RfcSecret).ShouldBe(
            "otpauth://totp/Okul%20Y%C3%B6netim:ATATURK-IST%3Ayonetici%40ataturk.test?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&issuer=Okul%20Y%C3%B6netim&digits=6&period=30");
        Totp.NewSecret().Length.ShouldBe(20);
        Totp.NewSecret().ShouldNotBe(Totp.NewSecret());
    }
}
