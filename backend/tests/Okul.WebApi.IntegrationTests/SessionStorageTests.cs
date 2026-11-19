using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0003 — Requirement 8 / AC-8, and the CSRF rule of plan 0003, R5. Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class SessionStorageTests
{
    private readonly ApiFixture _api;

    public SessionStorageTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task WebCookies_AreHttpOnlySecureStrict_NoTokenInBody()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _api.Testing.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            InstitutionSampleData.WithCampusesCode, IdentitySampleData.Teacher.Email, _api.SamplePassword, "web"), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        cookies.Count.ShouldBe(2);
        foreach (var name in new[] { SessionCookies.Access, SessionCookies.Refresh })
        {
            var cookie = cookies.Single(c => c.StartsWith(name + "=", StringComparison.Ordinal)).ToLowerInvariant();
            cookie.ShouldContain("httponly");
            cookie.ShouldContain("secure");
            cookie.ShouldContain("samesite=strict");
        }

        var body = (await response.Content.ReadFromJsonAsync<SessionResponse>(ct))!;
        body.AccessToken.ShouldBeNull();
        body.RefreshToken.ShouldBeNull();
        var raw = await response.Content.ReadAsStringAsync(ct);
        foreach (var cookie in cookies)
        {
            raw.ShouldNotContain(cookie.Split(';')[0].Split('=', 2)[1]);
        }
    }

    [Fact]
    public async Task CookieRequest_WithoutCsrfHeader_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var web = await _api.LoginWebAsync(IdentitySampleData.Teacher);
        web.DefaultRequestHeaders.Remove(SessionCookies.CsrfHeader);

        (await web.PostAsync("/api/v1/auth/logout", null, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await web.PostAsync("/api/v1/auth/refresh", null, ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await web.GetAsync("/api/v1/institution/me", ct)).StatusCode.ShouldBe(HttpStatusCode.OK); // reads need no header
    }

    [Fact]
    public async Task PasswordNeverInLogsOrErrors()
    {
        var ct = TestContext.Current.CancellationToken;
        var logs = new CapturingLoggerProvider();
        await using var factory = _api.CreateFactory(DevAuthEndpoints.TestingEnvironment)
            .WithWebHostBuilder(builder => builder.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs)));
        const string wrongPassword = "bu-sifre-loga-dusmemeli-77";
        var client = factory.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            InstitutionSampleData.WithCampusesCode, IdentitySampleData.Teacher.Email, wrongPassword, "mobile"), ct);
        var right = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(
            InstitutionSampleData.WithCampusesCode, IdentitySampleData.Teacher.Email, _api.SamplePassword, "mobile"), ct);

        (await wrong.Content.ReadAsStringAsync(ct)).ShouldNotContain(wrongPassword);
        (await right.Content.ReadAsStringAsync(ct)).ShouldNotContain(_api.SamplePassword);
        logs.Messages.ShouldNotBeEmpty();
        logs.Messages.ShouldAllBe(m => !m.Contains(wrongPassword) && !m.Contains(_api.SamplePassword));
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
