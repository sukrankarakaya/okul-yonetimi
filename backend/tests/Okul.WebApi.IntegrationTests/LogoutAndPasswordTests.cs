using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0003 — Requirements 6–7 / AC-6, AC-7 (G5: closing takes effect on the very next request).</summary>
[Trait("Category", "Integration")]
public sealed class LogoutTests
{
    private const string MineRoute = "/api/v1/institution/me";
    private readonly ApiFixture _api;

    public LogoutTests(ApiFixture api)
    {
        _api = api;
    }

    [Fact]
    public async Task AfterLogout_NextRequestAndRefresh_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var (mobile, session) = await _api.LoginMobileAsync(account);
        var web = await _api.LoginWebAsync(account);

        (await mobile.PostAsync("/api/v1/auth/logout", null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await web.PostAsync("/api/v1/auth/logout", null, ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The access token is still within its 15 minutes — and rejected anyway.
        (await mobile.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await web.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _api.Testing.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(session.RefreshToken), ct))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await web.PostAsync("/api/v1/auth/refresh", null, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OtherDeviceSession_StillWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var (phone, _) = await _api.LoginMobileAsync(account);
        var web = await _api.LoginWebAsync(account);

        await web.PostAsync("/api/v1/auth/logout", null, ct);

        (await phone.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

[Trait("Category", "Integration")]
public sealed class ChangePasswordTests
{
    private const string PasswordRoute = "/api/v1/auth/password";
    private const string NewPassword = "yepyeni-sifre-2026";
    private readonly ApiFixture _api;

    public ChangePasswordTests(ApiFixture api)
    {
        _api = api;
    }

    [Theory]
    [InlineData("yanlis-sifre-123", NewPassword, NewPassword, "Mevcut şifre hatalı.")]
    [InlineData(null, "kisa-1234", "kisa-1234", "Yeni şifre en az 10 karakter olmalı.")]
    [InlineData(null, NewPassword, "baska-sifre-2026", "Yeni şifreler eşleşmiyor.")]
    public async Task Rejects_WithTheSpecMessage_AndNothingChanges(string? current, string next, string again, string message)
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var (client, _) = await _api.LoginMobileAsync(account);

        var response = await client.PostAsJsonAsync(PasswordRoute,
            new ChangePasswordRequest(current ?? _api.SamplePassword, next, again), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(ct))!.Detail.ShouldBe(message);
        (await Login(account.Email, _api.SamplePassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Success_OldPasswordFails_OtherSessionsRejectedAtOnce_ThisOneStays()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var here = await _api.LoginWebAsync(account);
        var (other, _) = await _api.LoginMobileAsync(account);

        (await here.PostAsJsonAsync(PasswordRoute, new ChangePasswordRequest(_api.SamplePassword, NewPassword, NewPassword), ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await here.GetAsync("/api/v1/institution/me", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await other.GetAsync("/api/v1/institution/me", ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Login(account.Email, _api.SamplePassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Login(account.Email, NewPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _api.Testing.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(InstitutionSampleData.WithCampusesCode, email, password, "mobile"), TestContext.Current.CancellationToken);
}
