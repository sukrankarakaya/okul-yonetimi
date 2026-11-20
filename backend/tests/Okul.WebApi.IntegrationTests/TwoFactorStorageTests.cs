using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Okul.Modules.Identity.Domain;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;
using static Okul.WebApi.IntegrationTests.Infrastructure.TwoFactorSteps;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0004 — Requirement 10 / AC-11: the secret and the codes are never readable at rest or in any output.</summary>
[Trait("Category", "Integration")]
public sealed class TwoFactorStorageTests
{
    private readonly ApiFixture _api;

    public TwoFactorStorageTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task SecretAndCodes_NotReadableInDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock);
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: false, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "setup");
        var setup = await StartSetupAsync(client, step.Step);
        var codes = (await (await ConfirmAsync(client, step.Step, ApiFixture.CodeFromKey(setup.Key, clock.GetUtcNow())))
            .Content.ReadFromJsonAsync<SetupCompletedResponse>(ct))!.RecoveryCodes;

        var dump = await RawRowsAsync(admin.Id);

        dump.ShouldNotContain(setup.Key);
        dump.ShouldContain("v1:"); // there is a secret — encrypted
        foreach (var code in codes)
        {
            dump.ShouldNotContain(code);
            dump.ShouldNotContain(RecoveryCodeFormat.Normalize(code)!);
        }
    }

    [Fact]
    public async Task NeverInLogsOrErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment, clock)
            .WithWebHostBuilder(builder => builder.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs)));
        var admin = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId, setUpTwoFactor: false, StaffRoles.Administrator);
        var client = factory.CreateClient();
        var step = await StepAsync(client, _api, admin, "setup");
        var setup = await StartSetupAsync(client, step.Step);
        var wrong = await ConfirmAsync(client, step.Step, "000000");
        var confirmed = await ConfirmAsync(client, step.Step, ApiFixture.CodeFromKey(setup.Key, clock.GetUtcNow()));
        var codes = (await confirmed.Content.ReadFromJsonAsync<SetupCompletedResponse>(ct))!.RecoveryCodes;
        var verify = await StepAsync(client, _api, admin, "verify");
        var wrongRecovery = await VerifyAsync(client, verify.Step, null, codes[0] + "X");

        foreach (var answer in new[] { wrong, wrongRecovery })
        {
            var body = await answer.Content.ReadAsStringAsync(ct);
            body.ShouldNotContain(setup.Key);
            codes.ShouldAllBe(code => !body.Contains(code));
        }

        logs.Messages.ShouldNotBeEmpty();
        logs.Messages.ShouldAllBe(m => !m.Contains(setup.Key) && !codes.Any(m.Contains) && !m.Contains(verify.Step));
    }

    /// <summary>Every column of the account's 2FA rows as text — what a database copy would show.</summary>
    private async Task<string> RawRowsAsync(Guid accountId)
    {
        await using var scope = _api.Testing.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var rows = await db.Database.SqlQuery<string>($"""
            SELECT concat_ws('|', a.two_factor_secret, a.two_factor_last_step::text,
                   (SELECT string_agg(c.code_hash, ',') FROM kimlik.recovery_codes c WHERE c.account_id = a.id),
                   (SELECT string_agg(coalesce(l.pending_secret, '') || l.credential_hash, ',') FROM kimlik.login_challenges l WHERE l.account_id = a.id))
                   AS "Value"
            FROM kimlik.staff_accounts a WHERE a.id = {accountId}
            """).ToListAsync(TestContext.Current.CancellationToken);
        return rows.ShouldHaveSingleItem();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();

        public IReadOnlyCollection<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new Logger(_messages);

        public void Dispose()
        {
        }

        private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Enqueue(formatter(state, exception) + " " + exception);
        }
    }
}
