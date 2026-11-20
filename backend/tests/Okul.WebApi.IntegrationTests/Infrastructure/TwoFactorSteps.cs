using System.Net;
using System.Net.Http.Json;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Shouldly;

namespace Okul.WebApi.IntegrationTests.Infrastructure;

/// <summary>The spec 0004 login steps as a client makes them (mobile kind unless told otherwise).</summary>
public static class TwoFactorSteps
{
    public const string LoginRoute = "/api/v1/auth/login";
    public const string VerifyRoute = "/api/v1/auth/login/verify";
    public const string SetupRoute = "/api/v1/auth/login/setup";
    public const string ConfirmRoute = "/api/v1/auth/login/setup/confirm";
    public const string CancelRoute = "/api/v1/auth/login/cancel";
    public const string MineRoute = "/api/v1/institution/me";

    public static Task<HttpResponseMessage> PasswordAsync(
        HttpClient client, ApiFixture api, IdentitySampleData.SampleAccount account, string? password = null, string kind = "mobile") =>
        client.PostAsJsonAsync(LoginRoute, new LoginRequest(ApiFixture.CodeOf(account), account.Email, password ?? api.SamplePassword, kind),
            TestContext.Current.CancellationToken);

    /// <summary>The password step that must answer with a step of <paramref name="next"/> ("verify" or "setup").</summary>
    public static async Task<LoginStepResponse> StepAsync(
        HttpClient client, ApiFixture api, IdentitySampleData.SampleAccount account, string next, string kind = "mobile")
    {
        var response = await PasswordAsync(client, api, account, kind: kind);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse(); // a step is not a session
        var step = (await response.Content.ReadFromJsonAsync<LoginStepResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        step.Next.ShouldBe(next);
        return step;
    }

    public static Task<HttpResponseMessage> VerifyAsync(HttpClient client, string step, string? code, string? recoveryCode = null) =>
        client.PostAsJsonAsync(VerifyRoute, new VerifyLoginRequest(step, code, recoveryCode), TestContext.Current.CancellationToken);

    public static async Task<TwoFactorSetupResponse> StartSetupAsync(HttpClient client, string step)
    {
        var response = await client.PostAsJsonAsync(SetupRoute, new LoginStepRequest(step), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TwoFactorSetupResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    public static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string step, string? code) =>
        client.PostAsJsonAsync(ConfirmRoute, new VerifyLoginRequest(step, code, null), TestContext.Current.CancellationToken);

    /// <summary>The spec's answer: status, and the problem's code + text.</summary>
    public static async Task ShouldAnswerAsync(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        response.StatusCode.ShouldBe(status);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull().Detail.ShouldBe(detail);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    public static void UseBearer(HttpClient client, SessionResponse session) =>
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);
}
