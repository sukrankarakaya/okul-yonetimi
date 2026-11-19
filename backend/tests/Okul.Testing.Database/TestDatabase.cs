using System.Net.Sockets;
using Npgsql;

namespace Okul.Testing.Database;

/// <summary>
/// The per-run test database (spec 0002): created on a maintenance connection after the server checks (CB-4)
/// and the sweep of dead runs' databases (CB-3), dropped on dispose whether the tests passed or not (CB-2).
/// The same flow runs on the local server (<c>OKUL_TEST_PG</c>) and inside the container (plan 0002, R3).
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _maintenance;

    private TestDatabase(string maintenance, string name)
    {
        _maintenance = maintenance;
        Name = name;
        ConnectionString = new NpgsqlConnectionStringBuilder(maintenance) { Database = name }.ConnectionString;
    }

    public string Name { get; }

    /// <summary>Connection string of this run's database (same server and role as the maintenance connection).</summary>
    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync(
        string maintenance, TimeProvider time, Action<string> warn, CancellationToken cancellationToken = default)
    {
        await VerifyServerAsync(maintenance, cancellationToken);
        await SweepStaleAsync(maintenance, time.GetUtcNow(), warn, cancellationToken);

        var database = new TestDatabase(maintenance, TestDatabaseNames.NewName(time.GetUtcNow()));
        await ExecuteAsync(maintenance, $"CREATE DATABASE {Quote(database.Name)}", cancellationToken);
        return database;
    }

    /// <summary>
    /// Fails before any test runs when the server is unreachable or unfit. Messages name the server and the
    /// reason, never the password (PB-5).
    /// </summary>
    public static async Task VerifyServerAsync(string maintenance, CancellationToken cancellationToken = default)
    {
        int versionNum;
        bool hasCollation;
        try
        {
            await using var connection = new NpgsqlConnection(maintenance);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT current_setting('server_version_num')::int, "
                + "EXISTS (SELECT 1 FROM pg_collation WHERE collname = @collation)",
                connection);
            command.Parameters.AddWithValue("collation", ServerRequirements.Collation);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            versionNum = reader.GetInt32(0);
            hasCollation = reader.GetBoolean(1);
        }
        catch (Exception error) when (error is NpgsqlException or SocketException or TimeoutException)
        {
            throw new InvalidOperationException(
                $"Test veritabanı sunucusuna bağlanılamadı ({Describe(maintenance)}): {Scrub(error.Message, maintenance)}");
        }

        if (ServerRequirements.Problem(versionNum, hasCollation) is { } problem)
        {
            throw new InvalidOperationException($"{problem} ({Describe(maintenance)})");
        }
    }

    /// <summary>
    /// Drops every database whose name <see cref="TestDatabaseNames.IsStale"/> holds, closing its open
    /// connections. A stale database this role may not drop — another owner (plan 0002, R5), or a DROP the server
    /// refuses, e.g. a session of another role that FORCE may not end (review round 1, L-1; T-14) — is skipped
    /// with a warning; the run goes on. Returns the dropped names.
    /// </summary>
    public static async Task<IReadOnlyList<string>> SweepStaleAsync(
        string maintenance, DateTimeOffset now, Action<string> warn, CancellationToken cancellationToken = default)
    {
        var candidates = new List<(string Name, bool CanDrop)>();
        await using (var connection = new NpgsqlConnection(maintenance))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT d.datname, (pg_get_userbyid(d.datdba) = current_user "
                + "OR (SELECT r.rolsuper FROM pg_roles r WHERE r.rolname = current_user)) "
                + "FROM pg_database d WHERE starts_with(d.datname, @prefix)",
                connection);
            command.Parameters.AddWithValue("prefix", TestDatabaseNames.Prefix);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                candidates.Add((reader.GetString(0), reader.GetBoolean(1)));
            }
        }

        var dropped = new List<string>();
        foreach (var (name, canDrop) in candidates.Where(c => TestDatabaseNames.IsStale(c.Name, now)))
        {
            if (!canDrop)
            {
                warn($"Eski test veritabanı atlandı (sahibi başka bir rol): {name}");
                continue;
            }

            try
            {
                await ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {Quote(name)} WITH (FORCE)", cancellationToken);
                dropped.Add(name);
            }
            catch (PostgresException error)
            {
                warn($"Eski test veritabanı silinemedi, atlandı: {name} ({error.SqlState}: {error.MessageText})");
            }
        }

        return dropped;
    }

    public static async Task<bool> ExistsAsync(string maintenance, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(maintenance);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name)", connection);
        command.Parameters.AddWithValue("name", name);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static Task CreateNamedAsync(string maintenance, string name, CancellationToken cancellationToken = default) =>
        ExecuteAsync(maintenance, $"CREATE DATABASE {Quote(name)}", cancellationToken);

    public static Task DropNamedAsync(string maintenance, string name, CancellationToken cancellationToken = default) =>
        ExecuteAsync(maintenance, $"DROP DATABASE IF EXISTS {Quote(name)} WITH (FORCE)", cancellationToken);

    public async ValueTask DisposeAsync()
    {
        // Pooled idle connections to this database would otherwise linger after the drop.
        NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
        await DropNamedAsync(_maintenance, Name);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Identifiers come from <see cref="TestDatabaseNames"/> or tests; quoting keeps them literal.</summary>
    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string Describe(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"{builder.Host}:{builder.Port}, kullanıcı {builder.Username}, veritabanı {builder.Database}";
    }

    private static string Scrub(string message, string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString).Password is { Length: > 0 } password
            ? message.Replace(password, "***", StringComparison.Ordinal)
            : message;
}
