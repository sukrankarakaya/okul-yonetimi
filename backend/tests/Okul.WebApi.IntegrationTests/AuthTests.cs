using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.DevAuth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Sensitive area: Kimlik ve yetki.</summary>
[Trait("Category", "Integration")]
public sealed class AuthTests
{
    private const string MineRoute = "/api/v1/institution/me";

    // Fixed instants, no clock (docs/testing.md, Determinism): long expired / valid for any realistic run date.
    private static readonly DateTime LongAgo = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FarFuture = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly ApiFixture _api;

    public AuthTests(ApiFixture api)
    {
        _api = api;
    }

    /// <summary>AC-5 — no session, an expired session, or a forged token: 401 and no kurum data.</summary>
    [Fact]
    public async Task Unauthenticated_And_ExpiredToken_Return401_WithoutData()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = InstitutionSampleData.WithCampusesId;
        var noSession = _api.Testing.CreateClient();
        // An OPEN session behind the expired token: 401 can then only come from the lifetime check (spec 0003 review
        // round 1 #6 — a random session id would be refused by the session check even if lifetime validation broke).
        var openSession = await _api.OpenSessionAsync(tenant);
        var expired = ClientWithToken(Token(_api.SigningKey, tenant, notBefore: LongAgo, expires: LongAgo.AddMinutes(15), session: openSession));
        var forged = ClientWithToken(Token(RandomNumberGenerator.GetBytes(32), tenant, notBefore: LongAgo, expires: FarFuture));

        foreach (var client in new[] { noSession, expired, forged })
        {
            var response = await client.GetAsync(MineRoute, ct);

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            var body = await response.Content.ReadAsStringAsync(ct);
            body.ShouldNotContain(InstitutionSampleData.WithCampusesName);
            body.ShouldNotContain(tenant.ToString());
        }
    }

    /// <summary>
    /// Default deny: a valid signature on an OPEN session (spec 0003, G5) but without a Faz 1 role is not a tenant member.
    /// </summary>
    [Fact]
    public async Task TokenWithoutKnownRole_IsForbidden()
    {
        var tenant = InstitutionSampleData.WithCampusesId;
        var session = await _api.OpenSessionAsync(tenant);
        var client = ClientWithToken(Token(_api.SigningKey, tenant, LongAgo, FarFuture, role: "Student", session: session));

        var response = await client.GetAsync(MineRoute, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TestLogin_UnknownRole_IsRejected()
    {
        var client = _api.Testing.CreateClient();

        var response = await client.PostAsJsonAsync(
            DevAuthEndpoints.Route,
            new TestLoginRequest(InstitutionSampleData.WithCampusesId, "Student"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _api.Testing.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string Token(
        byte[] key, Guid tenant, DateTime notBefore, DateTime expires, string role = Roles.Administrator, Guid? session = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TokenSettings.Issuer,
            Audience = TokenSettings.Audience,
            IssuedAt = notBefore,
            NotBefore = notBefore,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(OkulClaims.Subject, Guid.CreateVersion7().ToString()),
                new Claim(OkulClaims.TenantId, tenant.ToString()),
                new Claim(OkulClaims.Role, role),
                new Claim(OkulClaims.Session, (session ?? Guid.CreateVersion7()).ToString()),
            ]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256),
        });
}
