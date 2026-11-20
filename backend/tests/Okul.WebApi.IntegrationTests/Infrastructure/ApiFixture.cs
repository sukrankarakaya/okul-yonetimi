using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Infrastructure.Persistence;
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

    /// <summary>The 2FA key for this run (spec 0004, plan R2) — every factory shares it, so a secret set up in one is read in another.</summary>
    public byte[] TwoFactorKey { get; } = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// The authenticator secret of every account the fixture sets 2FA up for (spec 0004, plan R5) — so a test can compute
    /// a valid code with <see cref="CodeAt"/>.
    /// </summary>
    public byte[] TwoFactorSecret { get; } = Totp.NewSecret();

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
            await IdentitySampleData.SeedAsync(
                identity, SamplePassword, scope.ServiceProvider.GetRequiredService<ITwoFactorSecretProtector>().Protect(TwoFactorSecret));

            // Spec 0007: the sample veliler and the tenant-free login state (plan 0007, A-1).
            var enrollment = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
            await enrollment.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<GuardianLoginDbContext>().Database.MigrateAsync();
            await EnrollmentSampleData.SeedAsync(enrollment, TimeProvider.System);
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
        new(environment, ConnectionString, Convert.ToBase64String(SigningKey), Convert.ToBase64String(TwoFactorKey), time, loginsPerMinute);

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

    /// <summary>
    /// A client signed in through the REAL staff login as the mobile app does (bearer) — spec 0003, AC-4. An account with
    /// 2FA completes the step with a kurtarma kodu made for this one login (plan 0004, R5): parallel tests logging in the
    /// same sample account never compete for one 30-second code. The code path itself is proven in the 2FA tests.
    /// </summary>
    public async Task<(HttpClient Client, SessionResponse Session)> LoginMobileAsync(
        IdentitySampleData.SampleAccount account, OkulApiFactory? factory = null)
    {
        var client = (factory ?? Testing).CreateClient();
        var session = await LoginAsync(client, account, "mobile", factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return (client, session);
    }

    /// <summary>A client signed in through the real login as the web panel does: httpOnly cookies + CSRF header.</summary>
    public async Task<HttpClient> LoginWebAsync(IdentitySampleData.SampleAccount account, OkulApiFactory? factory = null)
    {
        var client = WebClient(factory);
        await LoginAsync(client, account, "web", factory);
        return client;
    }

    private async Task<SessionResponse> LoginAsync(
        HttpClient client, IdentitySampleData.SampleAccount account, string clientKind, OkulApiFactory? factory)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            CodeOf(account), account.Email, SamplePassword, clientKind));
        response.EnsureSuccessStatusCode();
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return (await response.Content.ReadFromJsonAsync<SessionResponse>()).ShouldNotBeNull();
        }

        var step = (await response.Content.ReadFromJsonAsync<LoginStepResponse>()).ShouldNotBeNull();
        step.Next.ShouldBe("verify", "the account has no 2FA set up — use CreateAccountAsync(setUpTwoFactor: true)");
        var verified = await client.PostAsJsonAsync("/api/v1/auth/login/verify",
            new VerifyLoginRequest(step.Step, null, await AddRecoveryCodeAsync(account, factory)));
        verified.EnsureSuccessStatusCode();
        return (await verified.Content.ReadFromJsonAsync<SessionResponse>()).ShouldNotBeNull();
    }

    /// <summary>A valid 6-digit code of <see cref="TwoFactorSecret"/> at <paramref name="moment"/> (spec 0004).</summary>
    public string CodeAt(DateTimeOffset moment) => Totp.Compute(TwoFactorSecret, Totp.StepAt(moment));

    /// <summary>The code an authenticator app shows for a setup <paramref name="base32Key"/> at <paramref name="moment"/>.</summary>
    public static string CodeFromKey(string base32Key, DateTimeOffset moment)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in base32Key)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }

        return Totp.Compute(bytes.ToArray(), Totp.StepAt(moment));
    }

    /// <summary>Adds one unused kurtarma kodu to the account and returns it (test helper; plan 0004, R5).</summary>
    public async Task<string> AddRecoveryCodeAsync(IdentitySampleData.SampleAccount account, OkulApiFactory? factory = null)
    {
        var code = RecoveryCodeFormat.NewSet()[0];
        await using var scope = (factory ?? Testing).Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        using (provider.GetRequiredService<ITenantScope>().Begin(account.TenantId))
        {
            var db = provider.GetRequiredService<IdentityDbContext>();
            db.RecoveryCodes.Add(RecoveryCode.Create(Guid.CreateVersion7(), account.TenantId, account.Id,
                provider.GetRequiredService<IRecoveryCodeHasher>().Hash(RecoveryCodeFormat.Normalize(code)!)));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return code;
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
    public Task<IdentitySampleData.SampleAccount> CreateAccountAsync(Guid tenantId, params string[] roles) =>
        CreateAccountAsync(tenantId, setUpTwoFactor: null, roles);

    /// <param name="setUpTwoFactor">
    /// Null: set up with <see cref="TwoFactorSecret"/> when the roles make it mandatory (so 0003's tests log in as before);
    /// false: left without 2FA (setup tests); true: set up whatever the roles (spec 0004).
    /// </param>
    public async Task<IdentitySampleData.SampleAccount> CreateAccountAsync(Guid tenantId, bool? setUpTwoFactor, params string[] roles)
    {
        var account = new IdentitySampleData.SampleAccount(
            Guid.CreateVersion7(), tenantId, $"t-{Guid.NewGuid():N}@okul.test", roles.Length == 0 ? [StaffRoles.Teacher] : roles);
        await using var scope = Testing.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        using (provider.GetRequiredService<ITenantScope>().Begin(tenantId))
        {
            var accounts = provider.GetRequiredService<IStaffAccountStore>();
            var created = StaffAccount.Create(account.Id, tenantId, account.Email,
                provider.GetRequiredService<IPasswordHasher>().Hash(SamplePassword), account.Roles);
            if (setUpTwoFactor ?? created.TwoFactorRequired)
            {
                created.EnableTwoFactor(provider.GetRequiredService<ITwoFactorSecretProtector>().Protect(TwoFactorSecret), confirmedStep: 0);
            }

            await accounts.AddAsync(created, CancellationToken.None);
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
    private readonly string _twoFactorKey;
    private readonly TimeProvider? _time;
    private readonly int? _loginsPerMinute;

    public OkulApiFactory(
        string environment, string connectionString, string signingKey, string twoFactorKey, TimeProvider? time, int? loginsPerMinute)
    {
        _environment = environment;
        _connectionString = connectionString;
        _signingKey = signingKey;
        _twoFactorKey = twoFactorKey;
        _time = time;
        _loginsPerMinute = loginsPerMinute;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Okul", _connectionString);
        builder.UseSetting("Auth:SigningKey", _signingKey);
        builder.UseSetting("Auth:TwoFactorKey", _twoFactorKey);
        // Spec 0007: the veli login key, as the 2FA key; outside Development/Testing the API demands Netgsm (Requirement 5)
        // — these accounts are never called: no test outside Testing sends an SMS.
        builder.UseSetting("Auth:GuardianCodeKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        if (_environment is not ("Development" or DevAuthEndpoints.TestingEnvironment))
        {
            builder.UseSetting("Sms:Provider", "Netgsm");
            builder.UseSetting("Sms:Netgsm:UserCode", "test-yok");
            builder.UseSetting("Sms:Netgsm:Password", "test-yok");
            builder.UseSetting("Sms:Netgsm:SenderId", "TEST");
        }

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
