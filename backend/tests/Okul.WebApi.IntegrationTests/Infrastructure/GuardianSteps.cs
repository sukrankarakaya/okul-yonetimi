using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Okul.Modules.Enrollment.Domain;
using Okul.Modules.Enrollment.Infrastructure.Persistence;
using Okul.Modules.Enrollment.Infrastructure.Sms;
using Okul.WebApi.Auth;
using Shouldly;

namespace Okul.WebApi.IntegrationTests.Infrastructure;

/// <summary>
/// Spec 0007 — veli girişi steps through the REAL endpoints. Every test registers its own veli with a random telefon, so
/// parallel tests never share a pair's lock or request window.
/// </summary>
internal static class GuardianSteps
{
    public const string CodeRoute = "/api/v1/auth/guardian/code";
    public const string VerifyRoute = "/api/v1/auth/guardian/verify";
    public const string CancelRoute = "/api/v1/auth/guardian/cancel";
    public const string ConsentRoute = "/api/v1/guardian/consent";
    public const string MeRoute = "/api/v1/guardian/me";

    /// <summary>A random cep telefonu as a veli would type it (<c>05XX XXX XX XX</c>).</summary>
    public static string RandomPhone() => "05" + RandomNumberGenerator.GetString("0123456789", 9);

    public static string E164(string phone) => PhoneNumber.Parse(phone)!.E164;

    /// <summary>Registers a veli in the kurum (the kurum's veli telefon list — sample data in this spec, K5).</summary>
    public static async Task<string> RegisterGuardianAsync(
        this ApiFixture api, Guid tenantId, string? phone = null, string? name = "Test Veli", OkulApiFactory? factory = null)
    {
        phone ??= RandomPhone();
        await using var scope = (factory ?? api.Testing).Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EnrollmentDbContext>();
        db.Guardians.Add(Guardian.Register(Guid.CreateVersion7(), tenantId, PhoneNumber.Parse(phone)!, name, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return phone;
    }

    public static FakeSmsSender Sms(this OkulApiFactory factory) => factory.Services.GetRequiredService<FakeSmsSender>();

    /// <summary>Asks for a code and returns the one the fake SMS carried.</summary>
    public static async Task<string> RequestCodeAsync(this HttpClient client, OkulApiFactory factory, string kurum, string phone)
    {
        var response = await client.PostAsJsonAsync(CodeRoute, new GuardianCodeRequest(kurum, phone));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return factory.Sms().LastCodeFor(E164(phone)).ShouldNotBeNull();
    }

    public static Task<HttpResponseMessage> VerifyAsync(
        this HttpClient client, string kurum, string phone, string? code, string clientKind = "mobile") =>
        client.PostAsJsonAsync(VerifyRoute, new GuardianVerifyRequest(kurum, phone, code, clientKind));

    /// <summary>A mobile veli client with a bearer session; with <paramref name="consent"/> the rıza is given too.</summary>
    public static async Task<(HttpClient Client, GuardianSessionResponse Session)> LoginGuardianMobileAsync(
        this ApiFixture api, string kurum, string phone, bool consent = true, OkulApiFactory? factory = null)
    {
        factory ??= api.Testing;
        var client = factory.CreateClient();
        var code = await client.RequestCodeAsync(factory, kurum, phone);
        var response = await client.VerifyAsync(kurum, phone, code);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<GuardianSessionResponse>()).ShouldNotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Session.AccessToken);
        if (consent)
        {
            (await client.PostAsync(ConsentRoute, null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        return (client, session);
    }

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        return problem?.GetValueOrDefault("code")?.ToString();
    }

    /// <summary>The ProblemDetails "code" and "detail" in one read (a response body is read only once).</summary>
    public static async Task<(string? Code, string? Detail)> ProblemAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        return (problem?.GetValueOrDefault("code")?.ToString(), problem?.GetValueOrDefault("detail")?.ToString());
    }

    public static async Task<string?> ProblemDetailAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>();
        return problem?.GetValueOrDefault("detail")?.ToString();
    }
}
