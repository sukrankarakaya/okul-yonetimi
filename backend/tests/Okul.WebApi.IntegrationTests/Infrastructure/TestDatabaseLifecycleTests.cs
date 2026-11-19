using Npgsql;
using Okul.Testing.Database;
using Shouldly;

namespace Okul.WebApi.IntegrationTests.Infrastructure;

/// <summary>
/// Spec 0002 — the per-run database on this run's real server: the local PostgreSQL 18 when OKUL_TEST_PG is set,
/// the container otherwise (one flow, plan 0002 R3). Full profile.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TestDatabaseLifecycleTests
{
    private readonly ApiFixture _api;

    public TestDatabaseLifecycleTests(ApiFixture api)
    {
        _api = api;
    }

    private string Maintenance => _api.MaintenanceConnectionString;

    /// <summary>CB-2 — this run's own database exists under the naming rule; a new one is created and dropped on dispose.</summary>
    [Fact]
    public async Task RunDatabase_IsCreated_AndDroppedAtEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        TestDatabaseNames.CreatedAt(_api.DatabaseName).ShouldNotBeNull(_api.DatabaseName);
        (await TestDatabase.ExistsAsync(Maintenance, _api.DatabaseName, ct)).ShouldBeTrue();

        var database = await TestDatabase.CreateAsync(Maintenance, TimeProvider.System, _ => { }, ct);
        (await TestDatabase.ExistsAsync(Maintenance, database.Name, ct)).ShouldBeTrue();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync(ct);
        }

        await database.DisposeAsync();

        (await TestDatabase.ExistsAsync(Maintenance, database.Name, ct)).ShouldBeFalse();
    }

    /// <summary>CB-3 — 61 minutes: dropped even with an open connection; 59 minutes, unreadable and foreign names: kept.</summary>
    [Fact]
    public async Task Sweep_DropsStale_KeepsFreshAndForeign()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var stale = TestDatabaseNames.NewName(now.AddMinutes(-61));
        var fresh = TestDatabaseNames.NewName(now.AddMinutes(-59));
        var unreadable = $"okul_test_zaman_{suffix}";
        var foreign = $"baska_{suffix}";
        string[] kept = [fresh, unreadable, foreign];

        foreach (var name in kept.Prepend(stale))
        {
            await TestDatabase.CreateNamedAsync(Maintenance, name, ct);
        }

        try
        {
            await using var openOnStale = new NpgsqlConnection(
                new NpgsqlConnectionStringBuilder(Maintenance) { Database = stale, Pooling = false }.ConnectionString);
            await openOnStale.OpenAsync(ct);

            var dropped = await TestDatabase.SweepStaleAsync(Maintenance, now, _ => { }, ct);

            dropped.ShouldContain(stale);
            dropped.ShouldNotContain(fresh);
            (await TestDatabase.ExistsAsync(Maintenance, stale, ct)).ShouldBeFalse();
            foreach (var name in kept)
            {
                (await TestDatabase.ExistsAsync(Maintenance, name, ct)).ShouldBeTrue(name);
            }

            (await TestDatabase.ExistsAsync(Maintenance, _api.DatabaseName, ct)).ShouldBeTrue();
        }
        finally
        {
            foreach (var name in kept.Prepend(stale))
            {
                await TestDatabase.DropNamedAsync(Maintenance, name, ct);
            }
        }
    }

    /// <summary>
    /// CB-3, review round 1 L-1 — a stale database the server refuses to drop (here a template database, which no
    /// role may drop) is skipped with a warning; the sweep goes on and drops the other stale one.
    /// </summary>
    [Fact]
    public async Task Sweep_SkipsUndroppable_WithWarning_AndContinues()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var undroppable = TestDatabaseNames.NewName(now.AddMinutes(-90));
        var stale = TestDatabaseNames.NewName(now.AddMinutes(-90));
        var warnings = new List<string>();

        try
        {
            await TestDatabase.CreateNamedAsync(Maintenance, undroppable, ct);
            await TestDatabase.CreateNamedAsync(Maintenance, stale, ct);
            await ExecuteAsync($"ALTER DATABASE \"{undroppable}\" IS_TEMPLATE true", ct);

            var dropped = await TestDatabase.SweepStaleAsync(Maintenance, now, warnings.Add, ct);

            dropped.ShouldNotContain(undroppable);
            (await TestDatabase.ExistsAsync(Maintenance, undroppable, ct)).ShouldBeTrue();
            warnings.ShouldContain(w => w.Contains(undroppable, StringComparison.Ordinal));
            (await TestDatabase.ExistsAsync(Maintenance, stale, ct)).ShouldBeFalse();
        }
        finally
        {
            await ExecuteAsync(
                $"DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_database WHERE datname = '{undroppable}') THEN "
                + $"ALTER DATABASE \"{undroppable}\" IS_TEMPLATE false; END IF; END $$",
                ct);
            await TestDatabase.DropNamedAsync(Maintenance, undroppable, ct);
            await TestDatabase.DropNamedAsync(Maintenance, stale, ct);
        }
    }

    /// <summary>CB-4 + PB-5 — an unreachable server fails with host, port and user named, the password never.</summary>
    [Fact]
    public async Task UnreachableServer_FailsWithReason_WithoutPassword()
    {
        const string password = "gizli-parola-0002";
        var unreachable = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = 1,
            Username = "okul_test",
            Password = password,
            Database = "postgres",
            Timeout = 3,
        }.ConnectionString;

        var error = await Should.ThrowAsync<InvalidOperationException>(
            () => TestDatabase.CreateAsync(unreachable, TimeProvider.System, _ => { }, TestContext.Current.CancellationToken));

        error.Message.ShouldContain("bağlanılamadı");
        error.Message.ShouldContain("127.0.0.1:1");
        error.Message.ShouldContain("okul_test");
        error.Message.ShouldNotContain(password);
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(Maintenance);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }
}
