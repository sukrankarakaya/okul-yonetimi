using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Okul.Core.Tenancy;
using Okul.Modules.Identity.Application;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.Testing.Database;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Shouldly;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Okul.WebApi.IntegrationTests.Infrastructure.ApiFixture))]

namespace Okul.WebApi.IntegrationTests.Infrastructure;

/// <summary>
/// One fresh test database per run, dropped at the end — no data reaches the next run (docs/testing.md,
/// Resource hygiene 5; spec 0002). <c>OKUL_TEST_PG</c> set: on that local PostgreSQL 18 (ADR 0001); unset or
/// blank: inside a PostgreSQL 18 container that is disposed at the end. Both paths share one flow
/// (<see cref="TestDatabase"/>). Seeded with the two sample kurums of spec 0001 and the sample staff accounts of
/// spec 0003, whose password is random per run (never in the repo — plan 0003, R8).
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>The default factory's login limit is out of reach; the rate-limit test builds its own (plan 0003, R7).</summary>
    public const int UnlimitedLoginsPerMinute = 1_000_000;

    private PostgreSqlContainer? _container;
    private TestDatabase? _database;

    public byte[] SigningKey { get; } = RandomNumberGenerator.GetBytes(32);

    /// <summary>The sample accounts' password for this run.</summary>
    public string SamplePassword { get; } = "ornek-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

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
            var institutions = scope.ServiceProvider.GetRequiredService<InstitutionDbContext>();
            await institutions.Database.MigrateAsync();
            await InstitutionSampleData.SeedAsync(institutions);

            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await identity.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<LoginThrottleDbContext>().Database.MigrateAsync();
            await IdentitySampleData.SeedAsync(identity, SamplePassword);
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

    /// <param name="time">A fake clock for lifetime tests (plan 0003, R6); default the system clock.</param>
    /// <param name="loginsPerMinute">The source login limit (spec 0003, Requirement 4); default out of reach;
    /// <c>null</c> sets nothing, so the production default applies (VERIFY round 1 F-1).</param>
    public OkulApiFactory CreateFactory(string environment, TimeProvider? time = null, int? loginsPerMinute = UnlimitedLoginsPerMinute) =>
        new(environment, ConnectionString, Convert.ToBase64String(SigningKey), time, loginsPerMinute);

    /// <summary>A client signed in through the Testing-only test login (any role, incl. Guardian).</summary>
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

    /// <summary>A client signed in through the REAL staff login as the mobile app does (bearer) — spec 0003, AC-4.</summary>
    public async Task<(HttpClient Client, SessionResponse Session)> LoginMobileAsync(
        IdentitySampleData.SampleAccount account, OkulApiFactory? factory = null)
    {
        var client = (factory ?? Testing).CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            CodeOf(account), account.Email, SamplePassword, "mobile"));
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        session.ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return (client, session);
    }

    /// <summary>A client signed in through the real login as the web panel does: httpOnly cookies + CSRF header.</summary>
    public async Task<HttpClient> LoginWebAsync(IdentitySampleData.SampleAccount account, OkulApiFactory? factory = null)
    {
        var client = WebClient(factory);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            CodeOf(account), account.Email, SamplePassword, "web"));
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>The browser stand-in: HTTPS (Secure cookies), a cookie jar, and the CSRF header the web panel sends.</summary>
    public HttpClient WebClient(OkulApiFactory? factory = null)
    {
        var client = (factory ?? Testing).CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add(SessionCookies.CsrfHeader, "1");
        return client;
    }

    /// <summary>Opens a real session row for a hand-made token (tests of token shape, not of login).</summary>
    public async Task<Guid> OpenSessionAsync(Guid tenantId)
    {
        await using var scope = Testing.Services.CreateAsyncScope();
        var session = Session.Start(Guid.CreateVersion7(), tenantId, Guid.CreateVersion7(), ClientKind.Mobile,
            DateTimeOffset.UtcNow, RefreshCredential.HashOf(Guid.NewGuid().ToString("N")));
        using (scope.ServiceProvider.GetRequiredService<ITenantScope>().Begin(tenantId))
        {
            var sessions = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            await sessions.AddAsync(session, CancellationToken.None);
            await sessions.SaveChangesAsync(CancellationToken.None);
        }

        return session.Id;
    }

    /// <summary>A fresh account (random e-mail, <see cref="SamplePassword"/>) — for tests that lock or change it.</summary>
    public async Task<IdentitySampleData.SampleAccount> CreateAccountAsync(Guid tenantId, params string[] roles)
    {
        var account = new IdentitySampleData.SampleAccount(
            Guid.CreateVersion7(), tenantId, $"t-{Guid.NewGuid():N}@okul.test", roles.Length == 0 ? [StaffRoles.Teacher] : roles);
        await using var scope = Testing.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        using (provider.GetRequiredService<ITenantScope>().Begin(tenantId))
        {
            var accounts = provider.GetRequiredService<IStaffAccountStore>();
            await accounts.AddAsync(
                StaffAccount.Create(account.Id, tenantId, account.Email,
                    provider.GetRequiredService<IPasswordHasher>().Hash(SamplePassword), account.Roles),
                CancellationToken.None);
            await accounts.SaveChangesAsync(CancellationToken.None);
        }

        return account;
    }

    public static string CodeOf(IdentitySampleData.SampleAccount account) =>
        account.TenantId == InstitutionSampleData.WithCampusesId
            ? InstitutionSampleData.WithCampusesCode
            : InstitutionSampleData.WithoutCampusesCode;

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
    private readonly TimeProvider? _time;
    private readonly int? _loginsPerMinute;

    public OkulApiFactory(string environment, string connectionString, string signingKey, TimeProvider? time, int? loginsPerMinute)
    {
        _environment = environment;
        _connectionString = connectionString;
        _signingKey = signingKey;
        _time = time;
        _loginsPerMinute = loginsPerMinute;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Okul", _connectionString);
        builder.UseSetting("Auth:SigningKey", _signingKey);
        if (_loginsPerMinute is { } loginsPerMinute)
        {
            builder.UseSetting("Auth:LoginRateLimitPerMinute", loginsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (_time is not null)
        {
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton(_time)));
        }
    }
}
