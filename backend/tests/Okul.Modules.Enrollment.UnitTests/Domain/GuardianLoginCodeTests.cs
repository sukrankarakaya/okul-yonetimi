using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Enrollment.Domain;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Domain;

/// <summary>Spec 0007 — Requirement 3 (K2), AC-2: single use, 5 minutes, ended by a newer code or "Vazgeç" (G-1).</summary>
public sealed class GuardianLoginCodeTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static GuardianLoginCode Sent(DateTimeOffset now) =>
        GuardianLoginCode.Issue(Guid.CreateVersion7(), "pair", Guid.CreateVersion7(), "hash-123456", now);

    [Fact]
    public void RightCode_WithinFiveMinutes_IsAcceptedOnce()
    {
        var time = new FakeTimeProvider(Start);
        var code = Sent(time.GetUtcNow());

        time.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        code.Check("hash-123456", time.GetUtcNow()).ShouldBe(CodeCheck.Accepted);
        code.Check("hash-123456", time.GetUtcNow()).ShouldBe(CodeCheck.Wrong);
        code.IsPending(time.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void WrongCode_IsWrong_AndDoesNotUseTheCode()
    {
        var code = Sent(Start);

        code.Check("hash-000000", Start).ShouldBe(CodeCheck.Wrong);
        code.Check("hash-123456", Start).ShouldBe(CodeCheck.Accepted);
    }

    [Fact]
    public void AfterFiveMinutes_EvenTheRightCode_HasExpired()
    {
        var code = Sent(Start);

        code.Check("hash-123456", Start + GuardianLoginCode.Lifetime).ShouldBe(CodeCheck.Expired);
        code.IsPending(Start + GuardianLoginCode.Lifetime).ShouldBeFalse();
    }

    [Fact]
    public void EndedCode_NewerCodeOrVazgec_IsWrong()
    {
        var code = Sent(Start);
        code.End(Start.AddSeconds(10));

        code.Check("hash-123456", Start.AddSeconds(20)).ShouldBe(CodeCheck.Wrong);
        code.IsPending(Start.AddSeconds(20)).ShouldBeFalse();
    }

    [Fact]
    public void CodeOfAnUnregisteredPair_IsNeverAccepted_ButExpiresTheSameWay()
    {
        var ghost = GuardianLoginCode.Issue(Guid.CreateVersion7(), "pair", guardianId: null, "hash-123456", Start);

        ghost.Check("hash-123456", Start).ShouldBe(CodeCheck.Wrong);
        ghost.Check("hash-123456", Start + GuardianLoginCode.Lifetime).ShouldBe(CodeCheck.Expired);
    }

    [Theory]
    [InlineData("123456", "123456")]
    [InlineData(" 123 456 ", "123456")]
    [InlineData("12 34 56", "123456")]
    public void Format_IgnoresWhiteSpace(string input, string expected)
    {
        GuardianLoginCodeFormat.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("123-456")]
    public void Format_RejectsWhatIsNotSixDigits(string? input)
    {
        GuardianLoginCodeFormat.Normalize(input).ShouldBeNull();
    }

    [Fact]
    public void NewCode_IsSixDigits()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => GuardianLoginCodeFormat.New()).ToList();

        codes.ShouldAllBe(c => GuardianLoginCodeFormat.Normalize(c) == c);
        codes.Distinct().Count().ShouldBeGreaterThan(1);
    }
}

/// <summary>Spec 0007 — Requirement 9, AC-7: the login account is created on the first login and reused.</summary>
public sealed class GuardianTests
{
    [Fact]
    public void FirstLogin_CreatesTheAccount_LaterLoginsKeepIt()
    {
        var start = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var guardian = Guardian.Register(Guid.CreateVersion7(), Guid.CreateVersion7(), PhoneNumber.Parse("05321234567")!, " Ayşe ", start);

        guardian.HasAccount.ShouldBeFalse();
        guardian.RecordLogin(start.AddDays(1));
        guardian.RecordLogin(start.AddDays(2));

        guardian.HasAccount.ShouldBeTrue();
        guardian.AccountCreatedAt.ShouldBe(start.AddDays(1));
        guardian.Phone.ShouldBe("+905321234567");
        guardian.DisplayName.ShouldBe("Ayşe");
    }

    [Fact]
    public void WithoutAName_IsShownAsVeli()
    {
        var guardian = Guardian.Register(Guid.CreateVersion7(), Guid.CreateVersion7(), PhoneNumber.Parse("05321234567")!, "  ", DateTimeOffset.UnixEpoch);

        guardian.DisplayName.ShouldBe("Veli");
        Should.Throw<ArgumentException>(() =>
            Guardian.Register(Guid.Empty, Guid.CreateVersion7(), PhoneNumber.Parse("05321234567")!, null, DateTimeOffset.UnixEpoch));
    }
}
