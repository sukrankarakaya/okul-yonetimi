using Okul.Modules.Enrollment.Domain;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Domain;

/// <summary>Spec 0007 — Requirement 1 (telefon normalize), 11 (masked), AC-1.</summary>
public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("0532 123 45 67")]
    [InlineData("05321234567")]
    [InlineData("5321234567")]
    [InlineData("532-123-45-67")]
    [InlineData("+90 532 123 45 67")]
    [InlineData("+90 (532) 123 45 67")]
    [InlineData("+905321234567")]
    [InlineData("905321234567")]
    [InlineData("00905321234567")]
    [InlineData("  0532.123.45.67  ")]
    public void SameNumber_WrittenDifferently_IsOneNumber(string input)
    {
        var phone = PhoneNumber.Parse(input);

        phone.ShouldNotBeNull();
        phone.E164.ShouldBe("+905321234567");
        phone.ShouldBe(PhoneNumber.Parse("0532 123 45 67"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0212 123 45 67")]   // sabit hat
    [InlineData("532 123 45 6")]     // 9 hane
    [InlineData("0532 123 45 678")]  // 11 hane
    [InlineData("+1 532 123 45 67")] // yabancı
    [InlineData("+49 5321234567")]
    [InlineData("0532 123 45 6a")]
    [InlineData("++905321234567")]
    [InlineData("0532+1234567")]
    [InlineData("٠٥٣٢١٢٣٤٥٦٧")]       // ASCII olmayan rakamlar
    public void WhatIsNotATurkishMobile_IsNoNumber(string? input)
    {
        PhoneNumber.Parse(input).ShouldBeNull();
    }

    [Fact]
    public void Masked_ShowsOnlyTheLastTwoDigits()
    {
        PhoneNumber.Parse("0532 123 45 67")!.Masked.ShouldBe("+90 5** *** ** 67");
        PhoneNumber.Parse("0532 123 45 67")!.ToString().ShouldBe("+905321234567");
    }

    [Fact]
    public void StoredValue_RoundTrips_AndRejectsGarbage()
    {
        PhoneNumber.FromE164("+905321234567").National.ShouldBe("5321234567");
        Should.Throw<ArgumentException>(() => PhoneNumber.FromE164("+12025550100"));
    }
}
