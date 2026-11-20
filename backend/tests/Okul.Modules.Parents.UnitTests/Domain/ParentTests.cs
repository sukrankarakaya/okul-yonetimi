using Okul.Modules.Parents.Domain;
using Shouldly;

namespace Okul.Modules.Parents.UnitTests.Domain;

public sealed class ParentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var parent = Parent.Create(TenantId, "+905551234567", "Ahmet Yılmaz");

        parent.TenantId.ShouldBe(TenantId);
        parent.PhoneNumber.ShouldBe("+905551234567");
        parent.Name.ShouldBe("Ahmet Yılmaz");
        parent.FailureCount.ShouldBe(0);
        parent.LastFailureAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidPhoneNumber_Throws(string phoneNumber)
    {
        var act = () => Parent.Create(TenantId, phoneNumber, null);
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void RecordFailure_IncrementsCount()
    {
        var parent = Parent.Create(TenantId, "+905551234567");
        var now = DateTimeOffset.UtcNow;

        parent.RecordFailure(now);
        parent.FailureCount.ShouldBe(1);
        parent.LastFailureAt.ShouldNotBeNull();

        parent.RecordFailure(now.AddSeconds(1));
        parent.FailureCount.ShouldBe(2);
    }

    [Fact]
    public void IsLockedOut_WithinLockDuration_ReturnsTrue()
    {
        var parent = Parent.Create(TenantId, "+905551234567");
        var now = DateTimeOffset.UtcNow;

        parent.RecordFailure(now);

        parent.IsLockedOut(now.AddSeconds(5), TimeSpan.FromMinutes(15)).ShouldBeTrue();
    }

    [Fact]
    public void IsLockedOut_AfterLockDuration_ReturnsFalse()
    {
        var parent = Parent.Create(TenantId, "+905551234567");
        var now = DateTimeOffset.UtcNow;

        parent.RecordFailure(now);

        parent.IsLockedOut(now.AddMinutes(16), TimeSpan.FromMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void IsLockedOut_NoFailure_ReturnsFalse()
    {
        var parent = Parent.Create(TenantId, "+905551234567");
        var now = DateTimeOffset.UtcNow;

        parent.IsLockedOut(now, TimeSpan.FromMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void ResetFailures_ClearsCountAndTime()
    {
        var parent = Parent.Create(TenantId, "+905551234567");
        var now = DateTimeOffset.UtcNow;

        parent.RecordFailure(now);
        parent.FailureCount.ShouldBe(1);

        parent.ResetFailures();
        parent.FailureCount.ShouldBe(0);
        parent.LastFailureAt.ShouldBeNull();
    }
}
