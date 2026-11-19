using Npgsql;
using Okul.Testing.Database;
using Shouldly;

namespace Okul.WebApi.UnitTests.TestDatabase;

/// <summary>Spec 0002, CB-1 and PB-5 — pure, fast profile (no trait).</summary>
public sealed class TestDatabaseSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Empty_Or_Whitespace_MeansUnset(string? value)
    {
        TestDatabaseSettings.FromValue(value).ShouldBeNull();
    }

    [Fact]
    public void Uri_IsConvertedTo_MaintenanceConnection()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            TestDatabaseSettings.FromValue("  postgresql://okul_test@localhost:5433/postgres  "));

        connection.Host.ShouldBe("localhost");
        connection.Port.ShouldBe(5433);
        connection.Username.ShouldBe("okul_test");
        connection.Database.ShouldBe("postgres");
        connection.Password.ShouldBeNull();
    }

    [Fact]
    public void Uri_Defaults_PortAndDatabase_AndReadsEscapedPassword()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            TestDatabaseSettings.FromValue("postgres://okul_test:p%40ss%3Aw@db.local"));

        connection.Port.ShouldBe(5432);
        connection.Database.ShouldBe("postgres");
        connection.Password.ShouldBe("p@ss:w");
    }

    [Theory]
    [InlineData("Host=localhost;Username=okul_test;Password=gizli-parola")]
    [InlineData("mysql://okul_test:gizli-parola@localhost/postgres")]
    [InlineData("postgresql://okul_test:gizli-parola@localhost/postgres?sslmode=require")]
    public void InvalidValue_FailsWithReason_WithoutEchoingIt(string value)
    {
        var error = Should.Throw<InvalidOperationException>(() => TestDatabaseSettings.FromValue(value));

        error.Message.ShouldContain(TestDatabaseSettings.VariableName);
        error.Message.ShouldNotContain("gizli-parola");
    }
}
