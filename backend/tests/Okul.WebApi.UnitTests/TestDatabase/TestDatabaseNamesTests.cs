using System.Text.RegularExpressions;
using Okul.Testing.Database;
using Shouldly;

namespace Okul.WebApi.UnitTests.TestDatabase;

/// <summary>Spec 0002, CB-2 and CB-3 — pure, fast profile (no trait).</summary>
public sealed class TestDatabaseNamesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewName_HasPrefix_UtcSeconds_AndShortSuffix()
    {
        var local = new DateTimeOffset(2026, 11, 19, 15, 4, 5, TimeSpan.FromHours(3));

        var name = TestDatabaseNames.NewName(local);

        Regex.IsMatch(name, "^okul_test_20261119120405_[0-9a-f]{8}$").ShouldBeTrue(name);
        TestDatabaseNames.CreatedAt(name).ShouldBe(new DateTimeOffset(2026, 11, 19, 12, 4, 5, TimeSpan.Zero));
        TestDatabaseNames.NewName(local).ShouldNotBe(name);
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, false)]
    [InlineData(61, true)]
    [InlineData(24 * 60, true)]
    public void IsStale_Boundaries(int minutesAgo, bool stale)
    {
        var name = TestDatabaseNames.NewName(Now.AddMinutes(-minutesAgo));

        TestDatabaseNames.IsStale(name, Now).ShouldBe(stale);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("okul")]
    [InlineData("okul_test")]
    [InlineData("okul_test_")]
    [InlineData("OKUL_TEST_20000101000000_abcdef12")]
    [InlineData("okul_test_20001301000000_abcdef12")]
    [InlineData("okul_test_2000010100000_abcdef12")]
    [InlineData("okul_test_20000101000000")]
    [InlineData("okul_test_20000101000000_abcdefgh")]
    [InlineData("okul_test_20000101000000_abcdef12_extra")]
    [InlineData("benim_okul_test_20000101000000_abcdef12")]
    public void IsStale_IgnoresForeignAndUnreadableNames(string name)
    {
        TestDatabaseNames.IsStale(name, Now).ShouldBeFalse();
    }
}
