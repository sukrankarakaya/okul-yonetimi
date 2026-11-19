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
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Shouldly;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Okul.WebApi.IntegrationTests.Infrastructure.ApiFixture))]

namespace Okul.WebApi.IntegrationTests.Infrastructure;

/// <summary>
/// One fresh PostgreSQL 18 container per test run, disposed at the end — no data reaches the next
/// run (docs/testing.md, Resource hygiene 5). Seeded with the two sample kurums of spec 0001 and the sample staff
/// accounts of spec 0003, whose password is random per run (never in the repo — plan 0003, R8).
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>The default factory's login limit is out of reach; the rate-limit test builds its own (plan 0003, R7).</summary>
    public const int UnlimitedLoginsPerMinute = 1_000_000;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18").Build();

    public byte[] SigningKey { get; } = RandomNumberGenerator.GetBytes(32);

    /// <summary>The sample accounts' password for this run.</summary>
    public string SamplePassword { get; } = "ornek-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    public OkulApiFactory Testing { get; private set; } = null!;

    public string ConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
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

    /// <param name="time">A fake clock for lifetime tests (plan 0003, R6); default the system clock.</param>
    /// <param name="loginsPerMinute">The source login limit (spec 0003, Requirement 4); default out of reach.</param>
    public OkulApiFactory CreateFactory(string environment, TimeProvider? time = null, int loginsPerMinute = UnlimitedLoginsPerMinute) =>
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

    public async ValueTask DisposeAsync()
    {
        if (Testing is not null)
        {
            await Testing.DisposeAsync();
        }

        await _database.DisposeAsync();
    }
}

public sealed class OkulApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly string _connectionString;
    private readonly string _signingKey;
    private readonly TimeProvider? _time;
    private readonly int _loginsPerMinute;

    public OkulApiFactory(string environment, string connectionString, string signingKey, TimeProvider? time, int loginsPerMinute)
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
        builder.UseSetting("Auth:LoginRateLimitPerMinute", _loginsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (_time is not null)
        {
            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton(_time)));
        }
    }
}
