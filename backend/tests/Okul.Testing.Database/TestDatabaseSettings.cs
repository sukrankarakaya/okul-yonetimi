using Npgsql;

namespace Okul.Testing.Database;

/// <summary>
/// Reads <c>OKUL_TEST_PG</c> (spec 0002, CB-1): a libpq URI of a maintenance database on a local PostgreSQL 18,
/// e.g. <c>postgresql://okul_test@localhost:5432/postgres</c>. The same value feeds <c>pg_isready -d</c> in
/// scripts/check.conf, so it stays in libpq form and is converted here. The password belongs in pgpass, not in
/// the URI (plan 0002, R2); a password in the URI still works. Empty or whitespace = not set → container path.
/// </summary>
public static class TestDatabaseSettings
{
    public const string VariableName = "OKUL_TEST_PG";

    private const int DefaultPort = 5432;
    private const string DefaultDatabase = "postgres";

    /// <summary>The Npgsql maintenance connection string, or null when the variable is not set.</summary>
    public static string? FromEnvironment() => FromValue(Environment.GetEnvironmentVariable(VariableName));

    public static string? FromValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ToNpgsql(value.Trim());

    /// <summary>Error messages never echo the value: it may carry a password (PB-5).</summary>
    private static string ToNpgsql(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != "postgresql" && uri.Scheme != "postgres")
            || string.IsNullOrEmpty(uri.Host))
        {
            throw new InvalidOperationException(
                $"{VariableName} bir libpq URI'si olmalı: postgresql://kullanıcı@host:port/veritabanı");
        }

        if (uri.Query.Length > 0)
        {
            throw new InvalidOperationException(
                $"{VariableName}: URI parametreleri (?...) desteklenmiyor; yalnızca kullanıcı, host, port ve veritabanı.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 0 ? DefaultPort : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')) is { Length: > 0 } db ? db : DefaultDatabase,
        };

        var userInfo = uri.UserInfo;
        if (userInfo.Length > 0)
        {
            var colon = userInfo.IndexOf(':', StringComparison.Ordinal);
            builder.Username = Uri.UnescapeDataString(colon < 0 ? userInfo : userInfo[..colon]);
            if (colon >= 0)
            {
                builder.Password = Uri.UnescapeDataString(userInfo[(colon + 1)..]);
            }
        }

        return builder.ConnectionString;
    }
}
