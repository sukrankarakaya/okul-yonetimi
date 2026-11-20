using Okul.Modules.Parents.Infrastructure;
using Shouldly;

namespace Okul.Modules.Parents.UnitTests.Infrastructure;

public sealed class MockSmsProviderTests
{
    private readonly MockSmsProvider _provider = new();

    [Fact]
    public async Task SendCodeAsync_ValidCode_Returns_Success()
    {
        var result = await _provider.SendCodeAsync("+905551234567", "123456", CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task SendCodeAsync_StoresCode()
    {
        const string phoneNumber = "+905551234567";
        const string code = "123456";

        await _provider.SendCodeAsync(phoneNumber, code, CancellationToken.None);

        _provider.GetLastSentCode(phoneNumber).ShouldBe(code);
    }

    [Fact]
    public async Task SendCodeAsync_MultiplePhoneNumbers_EachStored()
    {
        await _provider.SendCodeAsync("+905551234567", "111111", CancellationToken.None);
        await _provider.SendCodeAsync("+905559876543", "222222", CancellationToken.None);

        _provider.GetLastSentCode("+905551234567").ShouldBe("111111");
        _provider.GetLastSentCode("+905559876543").ShouldBe("222222");
    }

    [Fact]
    public async Task SendCodeAsync_UpdatesCode_WhenSentMultipleTimes()
    {
        const string phoneNumber = "+905551234567";

        await _provider.SendCodeAsync(phoneNumber, "111111", CancellationToken.None);
        await _provider.SendCodeAsync(phoneNumber, "222222", CancellationToken.None);

        _provider.GetLastSentCode(phoneNumber).ShouldBe("222222");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendCodeAsync_EmptyPhoneNumber_Throws(string phoneNumber)
    {
        var act = () => _provider.SendCodeAsync(phoneNumber, "123456", CancellationToken.None);
        await act.ShouldThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendCodeAsync_EmptyCode_Throws(string code)
    {
        var act = () => _provider.SendCodeAsync("+905551234567", code, CancellationToken.None);
        await act.ShouldThrowAsync<ArgumentException>();
    }

    [Fact]
    public void GetLastSentCode_UnknownPhoneNumber_ReturnsNull()
    {
        _provider.GetLastSentCode("+905551234567").ShouldBeNull();
    }
}
