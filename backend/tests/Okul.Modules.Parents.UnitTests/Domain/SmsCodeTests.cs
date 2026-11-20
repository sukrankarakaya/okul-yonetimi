using Okul.Modules.Parents.Domain;
using Shouldly;

namespace Okul.Modules.Parents.UnitTests.Domain;

public sealed class SmsCodeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ParentId = Guid.NewGuid();

    [Fact]
    public void Create_ValidCode_Succeeds()
    {
        var code = SmsCode.Create(ParentId, TenantId, "123456", TimeSpan.FromMinutes(5));

        code.ParentId.ShouldBe(ParentId);
        code.TenantId.ShouldBe(TenantId);
        code.Code.ShouldBe("123456");
        code.AttemptCount.ShouldBe(0);
        code.IsExpired(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Theory]
    [InlineData("12345")]   // 5 digits
    [InlineData("1234567")] // 7 digits
    [InlineData("12345a")]  // non-digit
    [InlineData("")]
    public void Create_InvalidCode_Throws(string code)
    {
        var act = () => SmsCode.Create(ParentId, TenantId, code, TimeSpan.FromMinutes(5));
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void IsExpired_BeforeExpiry_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var code = SmsCode.Create(ParentId, TenantId, "123456", TimeSpan.FromMinutes(5));

        code.IsExpired(now).ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_AfterExpiry_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var code = SmsCode.Create(ParentId, TenantId, "123456", TimeSpan.FromSeconds(1));

        code.IsExpired(now.AddSeconds(2)).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_CorrectCodeBeforeExpiry_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var code = SmsCode.Create(ParentId, TenantId, "123456", TimeSpan.FromMinutes(5));

        code.IsValid("123456", now).ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WrongCode_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var code = SmsCode.Create(ParentId, TenantId, "123456", TimeSpan.FromMinutes(5));

        code.IsValid("654321", now).ShouldBeFalse();
    }

    [Fact]
    public void IsValid_ExpiredCode_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var code = SmsCode.Create(ParentId, TenantId, "123456", TimeSpan.FromSeconds(1));

        code.IsValid("123456", now.AddSeconds(2)).ShouldBeFalse();
    }

    [Fact]
    public void RecordAttempt_IncrementsCount()
    {
        var code = SmsCode.Create(ParentId, TenantId, "123456", TimeSpan.FromMinutes(5));

        code.RecordAttempt();
        code.AttemptCount.ShouldBe(1);

        code.RecordAttempt();
        code.AttemptCount.ShouldBe(2);
    }
}
