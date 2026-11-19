using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.Hosting;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>
/// docs/security.md, Web — headers on every response: a rejected request (401, authentication) and an
/// error response produced by the exception handler (404 "bulunamadı", AC-4) alike (T-12).
/// </summary>
[Trait("Category", "Integration")]
public sealed class SecurityHeadersTests
{
    private readonly ApiFixture _api;

    public SecurityHeadersTests(ApiFixture api)
    {
        _api = api;
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task RejectedRequest_CarriesSecurityHeaders(string environment)
    {
        await using var factory = _api.CreateFactory(environment);
        var response = await factory.CreateClient().GetAsync("/api/v1/institution/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        AssertSecurityHeaders(response);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task NotFoundFromExceptionHandler_CarriesSecurityHeaders(string environment)
    {
        var ct = TestContext.Current.CancellationToken;
        // Same signing key in every factory: a Testing-issued token is valid in Production too.
        var signedIn = await _api.ClientForAsync(InstitutionSampleData.WithCampusesId, Roles.Administrator);
        await using var factory = _api.CreateFactory(environment);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = signedIn.DefaultRequestHeaders.Authorization
            ?? throw new InvalidOperationException("no token");

        var response = await client.GetAsync($"/api/v1/campuses/{Guid.CreateVersion7()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        AssertSecurityHeaders(response);
    }

    /// <summary>HSTS only outside Development/Testing and only on HTTPS (TestServer: IsHttps from the scheme).</summary>
    [Theory]
    [InlineData("Production", "https://localhost", true)]
    [InlineData("Production", "http://localhost", false)]
    [InlineData("Testing", "https://localhost", false)]
    public async Task Hsts_OnlyOnHttps_OutsideDevelopment(string environment, string baseAddress, bool expectHsts)
    {
        await using var factory = _api.CreateFactory(environment);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(baseAddress),
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/v1/institution/me", TestContext.Current.CancellationToken);

        AssertSecurityHeaders(response);
        if (expectHsts)
        {
            response.Headers.GetValues("Strict-Transport-Security").ShouldBe([SecurityHeaders.StrictTransportSecurity]);
        }
        else
        {
            response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
        }
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").ShouldBe([SecurityHeaders.ContentSecurityPolicy]);
    }
}
