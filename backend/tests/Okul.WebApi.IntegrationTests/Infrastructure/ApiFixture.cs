using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.Testing.Database;
using Okul.WebApi.DevAuth;
using Shouldly;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Okul.WebApi.IntegrationTests.Infrastructure.ApiFixture))]

namespace Okul.WebApi.IntegrationTests.Infrastructure;

/// <summary>
/// One fresh test database per run, dropped at the end — no data reaches the next run (docs/testing.md,
/// Resource hygiene 5; spec 0002). <c>OKUL_TEST_PG</c> set: on that local PostgreSQL 18 (ADR 0001); unset or
/// blank: inside a PostgreSQL 18 container that is disposed at the end. Both paths share one flow
/// (<see cref="TestDatabase"/>). Seeded with the two sample kurums of spec 0001.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private TestDatabase? _database;

    public byte[] SigningKey { get; } = RandomNumberGenerator.GetBytes(32);

    public OkulApiFactory Testing { get; private set; } = null!;

    /// <summary>Maintenance connection of this run's server (local or container) — for the lifecycle tests.</summary>
    public string MaintenanceConnectionString { get; private set; } = null!;

    public string DatabaseName => _database!.Name;

    public string ConnectionString => _database!.ConnectionString;

    /// <summary>
    /// A failure after the database exists (migration, seed) cleans up here before rethrowing, so the run's
    /// database never outlives a failed start — whatever the test framework does next (review round 1, L-2; CB-2).
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        try
        {
            var maintenance = TestDatabaseSettings.FromEnvironment();
            if (maintenance is null)
            {
                _container = new PostgreSqlBuilder("postgres:18").Build();
                await _container.StartAsync();
                maintenance = _container.GetConnectionString();
            }

            MaintenanceConnectionString = maintenance;
            _database = await TestDatabase.CreateAsync(maintenance, TimeProvider.System, Console.Error.WriteLine);
            Testing = CreateFactory(DevAuthEndpoints.TestingEnvironment);

            await using var scope = Testing.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<InstitutionDbContext>();
            await db.Database.MigrateAsync();
            await InstitutionSampleData.SeedAsync(db);
        }
        catch
        {
            // The start failure is the one to show; a clean-up failure on top of it only goes to stderr (review round 2, L-2).
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanup)
            {
                await Console.Error.WriteLineAsync($"Test veritabanı temizliği de başarısız: {cleanup.Message}");
            }

            throw;
        }
    }

    public OkulApiFactory CreateFactory(string environment) =>
        new(environment, ConnectionString, Convert.ToBase64String(SigningKey));

    /// <summary>A client signed in through the Testing-only test login (the path the apps use).</summary>
    public async Task<HttpClient> ClientForAsync(Guid tenantId, string role)
    {
        var client = Testing.CreateClient();
        var response = await client.PostAsJsonAsync(DevAuthEndpoints.Route, new TestLoginRequest(tenantId, role));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<TestLoginResponse>();
        login.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    /// <summary>Each resource is released even when an earlier one fails; safe to call twice (L-2).</summary>
    public async ValueTask DisposeAsync()
    {
        var testing = Testing;
        var database = _database;
        var container = _container;
        Testing = null!;
        _database = null;
        _container = null;

        try
        {
            if (testing is not null)
            {
                await testing.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (database is not null)
                {
                    await database.DisposeAsync();
                }
            }
            finally
            {
                if (container is not null)
                {
                    await container.DisposeAsync();
                }
            }
        }
    }
}

public sealed class OkulApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly string _connectionString;
    private readonly string _signingKey;

    public OkulApiFactory(string environment, string connectionString, string signingKey)
    {
        _environment = environment;
        _connectionString = connectionString;
        _signingKey = signingKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Okul", _connectionString);
        builder.UseSetting("Auth:SigningKey", _signingKey);
    }
}
